using Microsoft.AspNetCore.Http;

namespace SaaSBarberia.Services;

public interface ITenantProvider
{
    int? CurrentTenantId { get; }
    bool EsSuperAdmin { get; }
}

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? CurrentTenantId
    {
        get
        {
            var claim = _httpContextAccessor?.HttpContext?.User?.FindFirst("TenantId")?.Value;
            return int.TryParse(claim, out var id) ? id : (int?)null;
        }
    }

    public bool EsSuperAdmin => _httpContextAccessor?.HttpContext?.User?.IsInRole("SuperAdmin") ?? false;
}
