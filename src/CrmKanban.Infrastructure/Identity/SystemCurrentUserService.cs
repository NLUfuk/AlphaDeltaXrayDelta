using CrmKanban.Application.Abstractions;

namespace CrmKanban.Infrastructure.Identity;

/// <summary>
/// Default <see cref="ICurrentUserService"/> used before the HTTP auth pipeline exists (Faz 2)
/// and for background/seed work. Two modes:
/// <list type="bullet">
///   <item><b>Anonymous</b> (default DI registration): no access, tenant filter returns nothing.</item>
///   <item><b>System</b> (<see cref="System"/>): SuperAdmin scope, bypasses the tenant filter so
///   the seeder can write global rows. Never wired to an HTTP request.</item>
/// </list>
/// In Faz 2 the API layer replaces the anonymous registration with an HttpContext-backed service.
/// </summary>
public sealed class SystemCurrentUserService : ICurrentUserService
{
    public static SystemCurrentUserService System { get; } = new(isSuperAdmin: true);

    private SystemCurrentUserService(bool isSuperAdmin) => IsSuperAdmin = isSuperAdmin;

    public SystemCurrentUserService() { } // anonymous default

    public Guid? UserId => null;
    public bool IsAuthenticated => false;
    public bool IsSuperAdmin { get; }
    public IReadOnlyCollection<Guid> CompanyIds { get; } = [];
}
