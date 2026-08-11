using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Infrastructure.Persistence;

// Implements IAppDbContext so Application services depend on the seam, not the concrete context.

/// <summary>
/// The single EF Core context. Tenant isolation is enforced here, in ONE place, by global query
/// filters (spec §6, §7): every company-scoped set is filtered to the caller's accessible
/// companies unless the caller is SuperAdmin. Application if-checks are a second line, never the
/// only one. Soft-deleted rows are filtered out everywhere.
/// </summary>
public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options, ICurrentUserService currentUser)
    : DbContext(options), IAppDbContext
{
    // Captured for the query filters. Referenced by the filter lambdas so they re-evaluate per instance.
    private bool IsSuperAdmin { get; } = currentUser.IsSuperAdmin;
    private Guid[] CompanyScope { get; } = [.. currentUser.CompanyIds];

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<CustomerTrust> CustomerTrusts => Set<CustomerTrust>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketStatus> TicketStatuses => Set<TicketStatus>();
    public DbSet<StatusTransition> StatusTransitions => Set<StatusTransition>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentRevision> CommentRevisions => Set<CommentRevision>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailQueue> EmailQueue => Set<EmailQueue>();
    public DbSet<UserNotificationPref> UserNotificationPrefs => Set<UserNotificationPref>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<FormField> FormFields => Set<FormField>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var b = modelBuilder;

        // ---- Company ----
        b.Entity<Company>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Website).HasMaxLength(300);
            e.Property(x => x.Address).HasMaxLength(500);
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.Id)));
        });

        // ---- User ---- (global identity; scoped through Membership, not a tenant filter)
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ---- Membership ----
        b.Entity<Membership>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CompanyId }).IsUnique();
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
        });

        // ---- CustomerTrust ----
        // Company-scoped like Membership: trust granted by one company must never read as trust at
        // another. The intake path reads it with IgnoreQueryFilters (an anonymous/customer caller has
        // no company scope at all) and therefore ALWAYS carries an explicit CompanyId predicate.
        b.Entity<CustomerTrust>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.UserId }).IsUnique();
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
        });

        // ---- Permission / RolePermission / UserPermission ----
        b.Entity<Permission>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Group).HasMaxLength(50).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });
        b.Entity<RolePermission>(e =>
        {
            e.HasIndex(x => new { x.Role, x.PermissionId }).IsUnique();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });
        b.Entity<UserPermission>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.PermissionId, x.CompanyId }).IsUnique();
            e.HasQueryFilter(x => x.DeletedAt == null
                && (IsSuperAdmin || x.CompanyId == null || CompanyScope.Contains(x.CompanyId.Value)));
        });

        // ---- RefreshToken / AuditLog ---- (not tenant-scoped)
        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });
        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(200).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });
        b.Entity<Invitation>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ---- Ticket + status machine ----
        b.Entity<Ticket>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.Number }).IsUnique();
            e.Property(x => x.Number).HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            // Money precision stated explicitly (Faz 39). EF's default for decimal happens to be the
            // same (18,2) — which is why no migration follows — but leaving it implicit means the day
            // someone changes a convention, amounts get silently truncated. EF warns about exactly this.
            e.Property(x => x.EstimatedValue).HasPrecision(18, 2);
            e.Property(x => x.ActualValue).HasPrecision(18, 2);
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
        });
        b.Entity<TicketStatus>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Color).HasMaxLength(20).IsRequired();
            // Global statuses (CompanyId null) are visible to everyone; company overrides are scoped.
            e.HasQueryFilter(x => x.DeletedAt == null
                && (IsSuperAdmin || x.CompanyId == null || CompanyScope.Contains(x.CompanyId.Value)));
        });
        b.Entity<StatusTransition>(e =>
        {
            e.HasIndex(x => new { x.FromStatusId, x.ToStatusId }).IsUnique();
            e.Property(x => x.AllowedByPermissionKey).HasMaxLength(100);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });
        b.Entity<Comment>(e =>
        {
            e.HasIndex(x => x.TicketId);
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
        });
        b.Entity<CommentRevision>(e =>
        {
            e.HasIndex(x => x.CommentId);
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
        });
        b.Entity<TicketEvent>(e =>
        {
            e.HasIndex(x => x.TicketId);
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
        });
        b.Entity<Attachment>(e =>
        {
            e.HasIndex(x => x.TicketId);
            e.HasIndex(x => x.S3Key).IsUnique();
            e.Property(x => x.S3Key).HasMaxLength(500).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(300).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
        });

        // ---- Notifications (not tenant-scoped: templates are global, queue is system-owned) ----
        b.Entity<EmailTemplate>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });
        b.Entity<EmailQueue>(e =>
        {
            e.HasIndex(x => x.Status);
            e.Property(x => x.ToEmail).HasMaxLength(256).IsRequired();
            e.Property(x => x.TemplateKey).HasMaxLength(100).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });
        b.Entity<UserNotificationPref>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.EventType }).IsUnique();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ---- Settings (global business params, super-admin managed — not tenant-scoped, §13) ----
        b.Entity<Setting>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(120).IsRequired();
            e.Property(x => x.Type).HasMaxLength(30).IsRequired();
            e.Property(x => x.Group).HasMaxLength(50).IsRequired();
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        // ---- FormField (per-company configurable public-form fields, §4.6 — tenant-scoped) ----
        b.Entity<FormField>(e =>
        {
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.Label).HasMaxLength(150).IsRequired();
            e.Property(x => x.Options).HasMaxLength(2000);
            e.HasQueryFilter(x => x.DeletedAt == null && (IsSuperAdmin || CompanyScope.Contains(x.CompanyId)));
        });
    }
}
