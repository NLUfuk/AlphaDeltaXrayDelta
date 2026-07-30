using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Abstractions;

/// <summary>
/// The persistence seam for Application services (spec §4.1 "bağımlılıklar abstraction üzerinden").
/// The DbContext is the Unit of Work and each DbSet is the repository — a per-entity repository layer
/// would be ceremony with no second implementation to justify it (SCOPE DISCIPLINE). Implemented by
/// CrmDbContext, faked/in-memory in tests.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Company> Companies { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserPermission> UserPermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DbSet<Ticket> Tickets { get; }
    DbSet<TicketStatus> TicketStatuses { get; }
    DbSet<StatusTransition> StatusTransitions { get; }
    DbSet<TicketCategory> TicketCategories { get; }
    DbSet<Comment> Comments { get; }
    DbSet<CommentRevision> CommentRevisions { get; }
    DbSet<TicketEvent> TicketEvents { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<EmailQueue> EmailQueue { get; }
    DbSet<UserNotificationPref> UserNotificationPrefs { get; }
    DbSet<Setting> Settings { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
