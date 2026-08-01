using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Notifications;

public sealed record EmailTemplateDto(string Key, string Subject, string Body, bool IsActive, DateTime? UpdatedAt);
public sealed record UpdateEmailTemplateRequest(string Subject, string Body);

/// <summary>
/// List/edit email templates from the UI (spec §14/§9). Bodies carry {{placeholder}} tokens filled at
/// send time. Super-admin only (templates are global, spec §8). Like Settings, update targets an existing
/// seeded key — it never creates arbitrary templates (the notification pipeline references them by key).
/// </summary>
public sealed class EmailTemplateService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<IReadOnlyList<EmailTemplateDto>> ListAsync(CancellationToken ct = default)
    {
        RequireSuperAdmin();
        return await db.EmailTemplates.OrderBy(t => t.Key)
            .Select(t => new EmailTemplateDto(t.Key, t.Subject, t.Body, t.IsActive, t.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(string key, UpdateEmailTemplateRequest request, CancellationToken ct = default)
    {
        RequireSuperAdmin();
        var actorId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        var template = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Key == key, ct)
            ?? throw new NotFoundException("template.unknown", $"Unknown email template '{key}'.");

        template.Update(request.Subject, request.Body);
        db.AuditLogs.Add(new AuditLog(actorId, "template.update", key));
        await db.SaveChangesAsync(ct);
    }

    private void RequireSuperAdmin()
    {
        if (!currentUser.IsSuperAdmin)
            throw new ForbiddenException("template.forbidden", "Only a super admin manages email templates.");
    }
}
