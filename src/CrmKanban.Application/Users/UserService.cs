using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Users;

/// <summary>
/// Super-admin user management (spec §9): create an admin account (invited, may open companies) and list
/// users for the permission-assignment UI. Admin accounts are never self-service (spec §18.5) — this is
/// the top of the onboarding chain: super admin → admin → (admin invites) personel/2nd admin.
/// </summary>
public sealed class UserService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IClock clock,
    IOptions<AuthOptions> authOptions)
{
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<InviteResult> CreateAdminAsync(CreateAdminRequest request, CancellationToken ct = default)
    {
        var actorId = RequireSuperAdmin();

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("user.email_taken", "A user with this email already exists.");

        var now = clock.UtcNow;
        var user = new User(email, request.FirstName, request.LastName);
        user.Deactivate();            // activated when the invite is accepted
        user.AllowCompanyCreation();  // this is what makes them an admin (spec §9)
        db.Users.Add(user);

        var raw = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var invitation = new Invitation(user.Id, HashToken(raw), now.AddDays(_auth.InviteTokenDays), actorId);
        db.Invitations.Add(invitation);
        db.AuditLogs.Add(new AuditLog(actorId, "admin.create", email));
        await db.SaveChangesAsync(ct);

        return new InviteResult(user.Id, raw, invitation.ExpiresAt);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search, CancellationToken ct = default)
    {
        RequireSuperAdmin();
        var q = db.Users.IgnoreQueryFilters().Where(u => u.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(u => u.Email.Contains(s) || u.FirstName.Contains(s) || u.LastName.Contains(s));
        }
        var users = await q.OrderBy(u => u.Email)
            .Select(u => new { u.Id, u.Email, Name = u.FirstName + " " + u.LastName, u.IsSuperAdmin, u.CanCreateCompany, u.IsActive })
            .Take(100).ToListAsync(ct);

        // Memberships for the listed users, with company names — the UI groups the table by company.
        var ids = users.Select(u => u.Id).ToList();
        var memberships = await (
            from m in db.Memberships.IgnoreQueryFilters().Where(m => ids.Contains(m.UserId) && m.DeletedAt == null)
            join c in db.Companies.IgnoreQueryFilters() on m.CompanyId equals c.Id
            select new { m.UserId, m.CompanyId, c.Name, m.Role }).ToListAsync(ct);
        var byUser = memberships.GroupBy(x => x.UserId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<UserCompanyDto>)g.Select(x => new UserCompanyDto(x.CompanyId, x.Name, (int)x.Role))
                .OrderBy(x => x.CompanyName).ToList());

        // Customers have no membership; their company relationship is "has a ticket there" (Faz 17).
        // Only resolved for users without a membership — staff are already grouped by their company.
        var customerIds = users.Where(u => !byUser.ContainsKey(u.Id)).Select(u => u.Id).ToList();
        var customerOf = customerIds.Count == 0
            ? []
            : (await (
                from t in db.Tickets.IgnoreQueryFilters().Where(t => customerIds.Contains(t.OpenedById) && t.DeletedAt == null)
                join c in db.Companies.IgnoreQueryFilters() on t.CompanyId equals c.Id
                select new { t.OpenedById, c.Name }).Distinct().ToListAsync(ct))
              .GroupBy(x => x.OpenedById)
              .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).OrderBy(n => n).ToList());

        return users.Select(u => new UserDto(u.Id, u.Email, u.Name, u.IsSuperAdmin, u.CanCreateCompany, u.IsActive,
            byUser.GetValueOrDefault(u.Id, []), customerOf.GetValueOrDefault(u.Id, []))).ToList();
    }

    private Guid RequireSuperAdmin()
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        if (!currentUser.IsSuperAdmin)
            throw new ForbiddenException("user.forbidden", "This action is super-admin only.");
        return userId;
    }

    private static string HashToken(string raw)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(bytes);
    }
}
