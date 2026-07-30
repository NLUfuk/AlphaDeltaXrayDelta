using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Kvkk;

/// <summary>
/// KVKK data-subject handling (spec §16). A deletion request is fulfilled as anonymization, not hard
/// delete: personal fields are masked while tickets/events/statistics and the audit chain are preserved.
/// Active refresh tokens are revoked so the masked account can't be used. Every anonymization is audited.
/// ponytail: super-admin only in v1 (a user can span companies; a single admin shouldn't erase identity
/// globally). Retention auto-purge (kvkk.retention_days is stored) is not run here — deferred until asked.
/// </summary>
public sealed class KvkkService(IAppDbContext db, ICurrentUserService currentUser, IClock clock)
{
    public async Task AnonymizeUserAsync(Guid userId, CancellationToken ct = default)
    {
        var actorId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        if (!currentUser.IsSuperAdmin)
            throw new ForbiddenException("kvkk.forbidden", "Anonymization is super-admin only.");

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("user.not_found", "User not found.");
        if (user.IsSuperAdmin)
            throw new ConflictException("kvkk.superadmin", "A super admin cannot be anonymized.");

        user.Anonymize();

        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(r => r.UserId == userId && r.RevokedAt == null).ToListAsync(ct);
        foreach (var token in tokens)
            token.Revoke(now);

        db.AuditLogs.Add(new AuditLog(actorId, "kvkk.anonymize", $"user {userId}"));
        await db.SaveChangesAsync(ct);
    }
}
