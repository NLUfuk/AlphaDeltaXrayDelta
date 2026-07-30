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
        return await q.OrderBy(u => u.Email)
            .Select(u => new UserDto(u.Id, u.Email, u.FirstName + " " + u.LastName, u.IsSuperAdmin, u.CanCreateCompany, u.IsActive))
            .Take(100).ToListAsync(ct);
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
