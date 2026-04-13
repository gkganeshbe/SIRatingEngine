using Dapper;
using Microsoft.AspNetCore.DataProtection;
using RatingEngine.Admin.Api;
using RatingEngine.Admin.Api.Endpoints;
using RatingEngine.Admin.Api.Services;
using RatingEngine.Core;
using RatingEngine.Data;
using RatingEngine.Data.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

SqlMapper.AddTypeHandler(DateOnlyTypeHandler.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRatingProblemDetails();

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
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITenantStore, SqlTenantStore>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// DB connection factory
builder.Services.AddScoped<DbConnectionFactory>();

// Admin repositories — both SQL Server and PostgreSQL concrete types registered;
// interfaces resolved at runtime via factory delegates based on tenant's connection string.

// SQL Server implementations
builder.Services.AddScoped<SqlProductAdminRepository>();
builder.Services.AddScoped<SqlCoverageAdminRepository>();
builder.Services.AddScoped<SqlCoverageRefAdminRepository>();
builder.Services.AddScoped<SqlLobAdminRepository>();
builder.Services.AddScoped<SqlRateTableAdminRepository>();
builder.Services.AddScoped<SqlPipelineStepAdminRepository>();
builder.Services.AddScoped<SqlColumnDefAdminRepository>();
builder.Services.AddScoped<SqlRiskFieldRepository>();
builder.Services.AddScoped<SqlPolicyAdjustmentAdminRepository>();
builder.Services.AddScoped<SqlProductStateAdminRepository>();
builder.Services.AddScoped<SqlLobScopeAdminRepository>();
builder.Services.AddScoped<SqlLookupDimensionAdminRepository>();
builder.Services.AddScoped<SqlDerivedKeyAdminRepository>();

// PostgreSQL implementations
builder.Services.AddScoped<PgSqlProductAdminRepository>();
builder.Services.AddScoped<PgSqlCoverageAdminRepository>();
builder.Services.AddScoped<PgSqlCoverageRefAdminRepository>();
builder.Services.AddScoped<PgSqlLobAdminRepository>();
builder.Services.AddScoped<PgSqlRateTableAdminRepository>();
builder.Services.AddScoped<PgSqlPipelineStepAdminRepository>();
builder.Services.AddScoped<PgSqlColumnDefAdminRepository>();
builder.Services.AddScoped<PgSqlRiskFieldRepository>();
builder.Services.AddScoped<PgSqlPolicyAdjustmentAdminRepository>();
builder.Services.AddScoped<PgSqlProductStateAdminRepository>();
builder.Services.AddScoped<PgSqlLobScopeAdminRepository>();
builder.Services.AddScoped<PgSqlLookupDimensionAdminRepository>();
builder.Services.AddScoped<PgSqlDerivedKeyAdminRepository>();

// Interface factory delegates — select implementation based on detected DB provider
builder.Services.AddScoped<IProductAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlProductAdminRepository>()
        : sp.GetRequiredService<SqlProductAdminRepository>());

builder.Services.AddScoped<ICoverageAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlCoverageAdminRepository>()
        : sp.GetRequiredService<SqlCoverageAdminRepository>());

builder.Services.AddScoped<ICoverageRefAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlCoverageRefAdminRepository>()
        : sp.GetRequiredService<SqlCoverageRefAdminRepository>());

builder.Services.AddScoped<ILobAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlLobAdminRepository>()
        : sp.GetRequiredService<SqlLobAdminRepository>());

builder.Services.AddScoped<IRateTableAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlRateTableAdminRepository>()
        : sp.GetRequiredService<SqlRateTableAdminRepository>());

builder.Services.AddScoped<IPipelineStepAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlPipelineStepAdminRepository>()
        : sp.GetRequiredService<SqlPipelineStepAdminRepository>());

builder.Services.AddScoped<IColumnDefAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlColumnDefAdminRepository>()
        : sp.GetRequiredService<SqlColumnDefAdminRepository>());

builder.Services.AddScoped<IRiskFieldRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlRiskFieldRepository>()
        : sp.GetRequiredService<SqlRiskFieldRepository>());

builder.Services.AddScoped<IPolicyAdjustmentAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlPolicyAdjustmentAdminRepository>()
        : sp.GetRequiredService<SqlPolicyAdjustmentAdminRepository>());

builder.Services.AddScoped<IProductStateAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlProductStateAdminRepository>()
        : sp.GetRequiredService<SqlProductStateAdminRepository>());

builder.Services.AddScoped<ILobScopeAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlLobScopeAdminRepository>()
        : sp.GetRequiredService<SqlLobScopeAdminRepository>());

builder.Services.AddScoped<ILookupDimensionAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlLookupDimensionAdminRepository>()
        : sp.GetRequiredService<SqlLookupDimensionAdminRepository>());

builder.Services.AddScoped<IDerivedKeyAdminRepository>(sp =>
    sp.GetRequiredService<DbConnectionFactory>().Provider == DatabaseProvider.PostgreSql
        ? sp.GetRequiredService<PgSqlDerivedKeyAdminRepository>()
        : sp.GetRequiredService<SqlDerivedKeyAdminRepository>());

// User management — proxies calls to the identity server's management API
builder.Services.AddHttpClient<UserManagementService>();

// Rating services — used by the Testing Sandbox endpoint
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPipelineFactory, JsonPipelineFactory>();
builder.Services.AddScoped<ICoverageConfigRepository, SqlCoverageConfigRepository>();
builder.Services.AddScoped<IRateLookupFactory, DbRateLookupFactory>();

// ── Authentication / Authorization ───────────────────────────────────────────
// Security:RequireAuth (appsettings.json) controls whether OIDC token validation
// is enforced.  Set to false for local development/testing; true for production.
var requireAuth = builder.Configuration.GetValue<bool>("Security:RequireAuth");

if (!requireAuth && !builder.Environment.IsDevelopment())
{
    // Fail-fast guard: Never allow auth bypass outside of Development
    throw new InvalidOperationException("CRITICAL SECURITY ERROR: 'Security:RequireAuth' cannot be false in non-development environments.");
}

if (requireAuth)
{
    var authority     = builder.Configuration["IdentityServer:Authority"]!;
    var adminClientId = builder.Configuration["IdentityServer:AdminApiClientId"]!;

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority            = authority;
            options.RequireHttpsMetadata = false;   // allow http identity servers (local / IIS)
            options.TokenValidationParameters = new()
            {
                ValidateIssuer   = false,  // issuer checked via discovery document
                ValidateAudience = false,  // audience varies by IS config; rely on scope claim instead
            };
        });
}
else
{
    builder.Services.AddAuthentication();
}

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
            policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            // Token uses allowed_apps / roles rather than a scope claim.
            // Grant access if the token permits the "rating" app OR carries a RATING_Admin role.
            policy.RequireAssertion(ctx =>
            {
                var allowedApps = ctx.User.FindAll("allowed_apps")
                    .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

                if (allowedApps.Contains("rating", StringComparer.OrdinalIgnoreCase))
                    return true;

                var roles = ctx.User.FindAll("roles")
                    .Concat(ctx.User.FindAll(System.Security.Claims.ClaimTypes.Role))
                    .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

                return roles.Contains("RATING_Admin", StringComparer.OrdinalIgnoreCase);
            });
        });
    }
});

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────────

// CORS must be first so preflight OPTIONS requests get the right headers
app.UseCors();
app.UseRatingProblemDetails();
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
adminGroup.MapRiskFieldEndpoints();
adminGroup.MapPolicyAdjustmentEndpoints();
adminGroup.MapLookupEndpoints();
adminGroup.MapTestEndpoints();
adminGroup.MapUserEndpoints();

app.Run();
