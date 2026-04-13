using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace RatingEngine.Admin.Api.Services;

// ── DTOs returned by the identity server management API ──────────────────────

public record UserSummary(
    string Id,
    string Email,
    string? FirstName,
    string? LastName,
    string[] Roles,
    DateTimeOffset CreatedAt);

public record CreateUserRequest(
    string Email,
    string Password,
    string? FirstName,
    string? LastName,
    string[] Roles);

public record ResetPasswordRequest(string NewPassword);

// ── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Calls the identity server's user management API on behalf of the Admin API.
/// Uses a client_credentials token (cached until near-expiry) so that every
/// request doesn't round-trip to the token endpoint.
/// </summary>
public class UserManagementService(
    HttpClient http,
    IConfiguration config,
    IMemoryCache cache,
    ILogger<UserManagementService> logger)
{
    private const string TokenCacheKey = "mgmt_access_token";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    // ── Token acquisition ─────────────────────────────────────────────────────

    private async Task<string> GetManagementTokenAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(TokenCacheKey, out string? cached)) return cached!;

        var authority = Authority();
        var clientId  = config["IdentityServer:ManagementClientId"]
                        ?? throw new InvalidOperationException("IdentityServer:ManagementClientId is not configured.");
        var secret    = config["IdentityServer:ManagementClientSecret"]
                        ?? throw new InvalidOperationException("IdentityServer:ManagementClientSecret is not configured.");
        var scope     = config["IdentityServer:ManagementScope"] ?? "user.management";

        var resp = await http.PostAsync(
            $"{authority}/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "client_credentials",
                ["client_id"]     = clientId,
                ["client_secret"] = secret,
                ["scope"]         = scope,
            }), ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            logger.LogError("Token acquisition failed ({Status}): {Body}", resp.StatusCode, body);
            throw new HttpRequestException($"Failed to acquire management token: {resp.StatusCode}");
        }

        var tokenDoc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
        var accessToken = tokenDoc!.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn   = tokenDoc.RootElement.TryGetProperty("expires_in", out var ei)
                          ? ei.GetInt32() : 3600;

        // Cache with 30-second buffer before actual expiry
        cache.Set(TokenCacheKey, accessToken, TimeSpan.FromSeconds(expiresIn - 30));
        return accessToken;
    }

    // ── User operations ───────────────────────────────────────────────────────

    public async Task<IEnumerable<UserSummary>> ListAsync(string tenantId, CancellationToken ct)
    {
        var req = AuthorizedRequest(HttpMethod.Get,
            $"{Authority()}/api/users?tenantId={Uri.EscapeDataString(tenantId)}",
            await GetManagementTokenAsync(ct));

        var resp = await http.SendAsync(req, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<UserSummary[]>(JsonOpts, ct))!;
    }

    public async Task<UserSummary> CreateAsync(CreateUserRequest request, string tenantId, CancellationToken ct)
    {
        // Attach tenantId as a top-level property — the identity server needs it
        // to scope the new account to the correct tenant.
        var payload = new
        {
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.Roles,
            TenantId = tenantId,
        };

        var req = AuthorizedRequest(HttpMethod.Post,
            $"{Authority()}/api/users",
            await GetManagementTokenAsync(ct));
        req.Content = JsonContent.Create(payload, options: JsonOpts);

        var resp = await http.SendAsync(req, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<UserSummary>(JsonOpts, ct))!;
    }

    public async Task DeleteAsync(string userId, string tenantId, CancellationToken ct)
    {
        var req = AuthorizedRequest(HttpMethod.Delete,
            $"{Authority()}/api/users/{Uri.EscapeDataString(userId)}?tenantId={Uri.EscapeDataString(tenantId)}",
            await GetManagementTokenAsync(ct));

        var resp = await http.SendAsync(req, ct);
        await EnsureSuccessAsync(resp, ct);
    }

    public async Task ResetPasswordAsync(string userId, ResetPasswordRequest request, string tenantId, CancellationToken ct)
    {
        var req = AuthorizedRequest(HttpMethod.Post,
            $"{Authority()}/api/users/{Uri.EscapeDataString(userId)}/reset-password",
            await GetManagementTokenAsync(ct));
        req.Content = JsonContent.Create(request, options: JsonOpts);

        var resp = await http.SendAsync(req, ct);
        await EnsureSuccessAsync(resp, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string Authority() =>
        (config["IdentityServer:Authority"]
         ?? throw new InvalidOperationException("IdentityServer:Authority is not configured."))
        .TrimEnd('/');

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string token)
    {
        var msg = new HttpRequestMessage(method, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return msg;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Identity server returned {(int)resp.StatusCode}: {body}",
                null,
                resp.StatusCode);
        }
    }
}
