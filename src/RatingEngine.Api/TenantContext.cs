using RatingEngine.Core;

namespace RatingEngine.Api;

/// <summary>
/// Scoped per-request. TenantMiddleware writes TenantId and ConnectionString;
/// everything else reads through the ITenantContext interface.
/// </summary>
public class TenantContext : ITenantContext
{
    public string TenantId { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
