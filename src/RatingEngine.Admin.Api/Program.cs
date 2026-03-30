using Dapper;
using Microsoft.AspNetCore.DataProtection;
using RatingEngine.Admin.Api;
using RatingEngine.Admin.Api.Endpoints;
using RatingEngine.Core;
using RatingEngine.Data;
using RatingEngine.Data.Admin;
using OpenIddict.Validation.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

SqlMapper.AddTypeHandler(DateOnlyTypeHandler.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ── CORS ───────────────────────────────────────────────────────────────────────
// Allow the Angular admin UI (any localhost port in development, locked down in production)
builder.Services.AddCors(options =>
{
    // Cors:AllowedOrigins in appsettings.json — add production origins there.
    // An empty/missing list falls back to allowing all localhost origins so the
    // Angular admin UI works out-of-the-box during local development.
    var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    options.AddDefaultPolicy(policy =>
    {
        if (configuredOrigins.Length > 0)
            policy.WithOrigins(configuredOrigins);
        else
            policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost");

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

// ── Data Protection ────────────────────────────────────────────────────────────
// Persist keys to a local folder so they survive app restarts and avoid the
// three "ephemeral / in-memory / unencrypted" startup warnings.
var keysDir = new DirectoryInfo(
    Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys"));

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(keysDir)
    .SetApplicationName("RatingEngine.Admin.Api");

// ── Services ───────────────────────────────────────────────────────────────────

// Tenant infrastructure
builder.Services.AddSingleton<ITenantStore, ConfigurationTenantStore>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// DB connection factory
builder.Services.AddScoped<DbConnectionFactory>();

// Admin repositories
builder.Services.AddScoped<IProductAdminRepository, SqlProductAdminRepository>();
builder.Services.AddScoped<ICoverageAdminRepository, SqlCoverageAdminRepository>();
builder.Services.AddScoped<IRateTableAdminRepository, SqlRateTableAdminRepository>();
builder.Services.AddScoped<IPipelineStepAdminRepository, SqlPipelineStepAdminRepository>();
builder.Services.AddScoped<IColumnDefAdminRepository, SqlColumnDefAdminRepository>();

// ── Authentication / Authorization ───────────────────────────────────────────
// Security:RequireAuth (appsettings.json) controls whether OIDC token validation
// is enforced.  Set to false for local development/testing; true for production.
var requireAuth = builder.Configuration.GetValue<bool>("Security:RequireAuth");

if (requireAuth)
{
    var authority     = builder.Configuration["IdentityServer:Authority"]!;
    var adminClientId = builder.Configuration["IdentityServer:AdminApiClientId"]!;
    var adminSecret   = builder.Configuration["IdentityServer:AdminApiClientSecret"]!;

    builder.Services.AddOpenIddict()
        .AddValidation(options =>
        {
            options.SetIssuer(authority);
            options.UseIntrospection()
                   .SetClientId(adminClientId)
                   .SetClientSecret(adminSecret);
            options.UseSystemNetHttp();
            options.UseAspNetCore();
        });
}

builder.Services.AddAuthentication();
builder.Services.AddAuthorization(options =>
{
    if (!requireAuth)
    {
        // Auth disabled — allow all requests (local / dev mode)
        options.AddPolicy("AdminAccess", policy => policy.RequireAssertion(_ => true));
    }
    else
    {
        options.AddPolicy("AdminAccess", policy =>
        {
            policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "rating-engine.admin");
        });
    }
});

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────────

// CORS must be first so preflight OPTIONS requests get the right headers
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

// ── Endpoints ──────────────────────────────────────────────────────────────────

var adminGroup = app.MapGroup("").RequireAuthorization("AdminAccess");
adminGroup.MapProductEndpoints();
adminGroup.MapCoverageEndpoints();
adminGroup.MapPipelineStepEndpoints();
adminGroup.MapRateTableEndpoints();
adminGroup.MapColumnDefEndpoints();

app.Run();
