using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace RatingEngine.Api;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
    public string[] ApiKeys { get; set; } = [];
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    private const string ApiKeyHeader = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var headerValues))
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Api-Key header"));

        var providedKey = headerValues.FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey) || !Options.ApiKeys.Contains(providedKey))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        // Include the scope claim so the shared QuoteAccess policy works unchanged.
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "api-key-client"),
            new Claim("scope", "quote.access")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
