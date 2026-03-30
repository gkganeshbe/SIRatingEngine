using RatingEngine.Core;

namespace RatingEngine.Admin.Api;

public class TenantContext : ITenantContext
{
    public string TenantId { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
