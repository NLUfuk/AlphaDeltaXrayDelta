using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Companies;

/// <summary>
/// Company lifecycle (spec §8, §9, §18.8, §18.20). An admin opens their own companies (owner + Admin
/// membership per company, no limit on how many); a super admin may open one on an admin's behalf.
/// Archive closes the intake; delete additionally hides the tenant — both are soft, nothing cascades.
/// Uniqueness of the slug is checked across ALL tenants (IgnoreQueryFilters) — the form link is global.
/// </summary>
public sealed class CompanyService(IAppDbContext db, ICurrentUserService currentUser, IClock clock)
{
    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");

        // Owner: an admin owns what they create; a super admin may target an existing admin account.
        Guid ownerId = userId;
        if (currentUser.IsSuperAdmin)
        {
            if (request.OwnerAdminId is { } target)
            {
                _ = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == target, ct)
                    ?? throw new NotFoundException("user.not_found", "Owner admin not found.");
                ownerId = target;
            }
        }
        else
        {
            var me = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (me is null || !me.CanCreateCompany)
                throw new ForbiddenException("company.create_forbidden", "You are not allowed to create companies.");
        }

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Slug == slug, ct))
            throw new ConflictException("company.slug_taken", "This slug is already in use.");

        var company = new Company(request.Name, slug, ownerId);
        company.UpdateInfo(request.Name, request.Phone, request.Email, request.Website, request.Address);
        db.Companies.Add(company);
        db.Memberships.Add(new Membership(ownerId, company.Id, RoleType.Admin));
        db.AuditLogs.Add(new AuditLog(userId, "company.create", $"{company.Name} ({slug}) owner {ownerId}"));
        await db.SaveChangesAsync(ct);

        return ToDto(company);
    }

    /// <summary>Companies the caller may see: super admin → all; otherwise the ones they are a member of
    /// (read from Memberships by user id, so a just-created company shows before the JWT refreshes).</summary>
    public async Task<IReadOnlyList<CompanyDto>> ListAsync(CancellationToken ct = default)
    {
        var q = db.Companies.IgnoreQueryFilters().Where(c => c.DeletedAt == null);
        if (!currentUser.IsSuperAdmin)
        {
            var userId = RequireUserId();
            var companyIds = await db.ActiveMemberships()
                .Where(m => m.UserId == userId).Select(m => m.CompanyId).ToListAsync(ct);
            q = q.Where(c => companyIds.Contains(c.Id));
        }
        return await q.OrderBy(c => c.Name).Select(c => ToDto(c)).ToListAsync(ct);
    }

    /// <summary>Edits the name + contact card. Same gate as archive: an Admin OF THIS COMPANY or a super
    /// admin — the company row is tenant data, so a member of another company must not reach it (the
    /// lookup runs with IgnoreQueryFilters to give a 404/403 instead of a confusing empty result).</summary>
    public async Task<CompanyDto> UpdateAsync(Guid companyId, UpdateCompanyRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == companyId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("company.not_found", "Company not found.");

        if (!currentUser.IsSuperAdmin && !await IsAdminOfAsync(userId, companyId, ct))
            throw new ForbiddenException("company.update_forbidden", "Only an admin of this company or a super admin can edit it.");

        company.UpdateInfo(request.Name, request.Phone, request.Email, request.Website, request.Address);
        db.AuditLogs.Add(new AuditLog(userId, "company.update", $"{company.Name} ({companyId})"));
        await db.SaveChangesAsync(ct);
        return ToDto(company);
    }

    public async Task ArchiveAsync(Guid companyId, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == companyId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("company.not_found", "Company not found.");

        if (!currentUser.IsSuperAdmin && !await IsAdminOfAsync(userId, companyId, ct))
            throw new ForbiddenException("company.archive_forbidden", "Only the owning admin or a super admin can archive a company.");

        company.Archive(clock.UtcNow);
        db.AuditLogs.Add(new AuditLog(userId, "company.archive", companyId.ToString()));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Deletes a company. Destructive, so it takes two gates: only the OWNER admin (not any
    /// company admin) or a super admin may call it, and the caller must echo the company name back —
    /// the UI's confirm step is the first gate, this is the second, server-side one.
    /// The row is soft-deleted (spec §18.20: no cascade, tickets/audit stay readable in the DB) and the
    /// company is retired, which closes every write path that guards on IsArchived/IsActive and frees
    /// the public slug. Memberships go with it so the tenant disappears from everyone's session.</summary>
    public async Task DeleteAsync(Guid companyId, DeleteCompanyRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == companyId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("company.not_found", "Company not found.");

        if (!currentUser.IsSuperAdmin && company.OwnerAdminId != userId)
            throw new ForbiddenException("company.delete_forbidden", "Only the owner admin or a super admin can delete a company.");

        if (!string.Equals(request.ConfirmName.Trim(), company.Name, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("company.delete_name_mismatch", "The confirmation name does not match the company name.");

        var memberships = await db.Memberships.IgnoreQueryFilters()
            .Where(m => m.CompanyId == companyId && m.DeletedAt == null).ToListAsync(ct);

        company.Retire(clock.UtcNow);
        db.Memberships.RemoveRange(memberships); // interceptor soft-deletes
        db.Companies.Remove(company);
        db.AuditLogs.Add(new AuditLog(userId, "company.delete", $"{company.Name} ({companyId}) members {memberships.Count}"));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Members (staff) of a company, for assignment/permission pickers. Caller must be a member or super admin.</summary>
    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid companyId, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        if (!currentUser.IsSuperAdmin && !await db.ActiveMemberships().AnyAsync(m => m.UserId == userId && m.CompanyId == companyId, ct))
            throw new ForbiddenException("company.members_forbidden", "You are not a member of this company.");

        // A super admin is never listed as a staff member — it must stay invisible in assignment/
        // permission pickers to everyone (spec §9: super admin sits above tenants, is not "staff").
        return await (from m in db.ActiveMemberships().Where(m => m.CompanyId == companyId)
                      join u in db.Users.IgnoreQueryFilters() on m.UserId equals u.Id
                      where !u.IsSuperAdmin
                      orderby u.FirstName
                      select new MemberDto(u.Id, u.Email, u.FirstName + " " + u.LastName, (int)m.Role)).ToListAsync(ct);
    }

    /// <summary>Remove a staff member from a company (spec §3). The owning admin of the company or a super
    /// admin only. The company owner can't be removed (that would orphan the company). Soft-deletes the
    /// membership (the interceptor turns Remove into a soft delete), so history and audit stay intact.</summary>
    public async Task RemoveMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        var callerId = RequireUserId();
        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == companyId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("company.not_found", "Company not found.");

        if (!currentUser.IsSuperAdmin && !await IsAdminOfAsync(callerId, companyId, ct))
            throw new ForbiddenException("company.member_remove_forbidden", "Only the owning admin or a super admin can remove members.");

        if (company.OwnerAdminId == userId)
            throw new BadRequestException("company.owner_immutable", "The company owner cannot be removed.");

        var membership = await db.ActiveMemberships()
            .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.UserId == userId, ct)
            ?? throw new NotFoundException("membership.not_found", "This user is not a member of the company.");

        db.Memberships.Remove(membership); // interceptor soft-deletes
        db.AuditLogs.Add(new AuditLog(callerId, "company.member_remove", $"company {companyId} user {userId}"));
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> IsAdminOfAsync(Guid userId, Guid companyId, CancellationToken ct) =>
        await db.ActiveMemberships()
            .AnyAsync(m => m.UserId == userId && m.CompanyId == companyId && m.Role == RoleType.Admin, ct);

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");

    private static CompanyDto ToDto(Company c) =>
        new(c.Id, c.Name, c.Slug, c.OwnerAdminId, c.IsActive, c.IsArchived, c.TicketNumberPrefix,
            c.Phone, c.Email, c.Website, c.Address);
}
