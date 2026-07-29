using System.Security.Claims;
using CrmKanban.Application.Abstractions;
using CrmKanban.Infrastructure.Identity;

namespace CrmKanban.Api.Auth;

/// <summary>
/// Builds <see cref="ICurrentUserService"/> from the validated JWT claims (spec §6). The tenant
/// filter reads this per request. company_id claims come only from the signed token — never from a
/// header or query — so a client cannot widen its own scope.
/// </summary>
public sealed class HttpCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue("sub"), out var id) ? id : null;

    public bool IsSuperAdmin =>
        string.Equals(User?.FindFirstValue(JwtTokenService.SuperAdminClaim), "true", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyCollection<Guid> CompanyIds =>
        User?.FindAll(JwtTokenService.CompanyClaim)
            .Select(c => Guid.TryParse(c.Value, out var g) ? g : (Guid?)null)
            .Where(g => g is not null)
            .Select(g => g!.Value)
            .ToArray() ?? [];
}
