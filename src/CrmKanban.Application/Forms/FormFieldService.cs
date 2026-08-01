using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Forms;

public sealed record FormFieldDto(Guid Id, string Label, int Type, bool Required, int SortOrder, string? Options, bool IsActive);
public sealed record PublicFormFieldDto(Guid Id, string Label, int Type, bool Required, IReadOnlyList<string> Options);
public sealed record CreateFormFieldRequest(string Label, int Type, bool Required, string? Options);
public sealed record UpdateFormFieldRequest(string Label, int Type, bool Required, int SortOrder, string? Options, bool IsActive);

/// <summary>
/// Manages a company's configurable public-form fields (spec §4.6). Admin of the company or a super admin
/// only — the same ownership gate as other company config (no dedicated permission key wired; see
/// PROGRESS decision log). The public read path (<see cref="ListActiveAsync"/>) is anonymous and returns
/// only the active fields, ordered for rendering.
/// </summary>
public sealed class FormFieldService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<IReadOnlyList<FormFieldDto>> ListForCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        await EnsureCompanyAdminAsync(companyId, ct);
        return await db.FormFields.IgnoreQueryFilters()
            .Where(f => f.CompanyId == companyId && f.DeletedAt == null)
            .OrderBy(f => f.SortOrder)
            .Select(f => new FormFieldDto(f.Id, f.Label, (int)f.Type, f.Required, f.SortOrder, f.Options, f.IsActive))
            .ToListAsync(ct);
    }

    /// <summary>Active fields for anonymous public-form rendering (no auth). Ordered for display.</summary>
    public async Task<IReadOnlyList<PublicFormFieldDto>> ListActiveAsync(Guid companyId, CancellationToken ct = default) =>
        await db.FormFields.IgnoreQueryFilters()
            .Where(f => f.CompanyId == companyId && f.DeletedAt == null && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .Select(f => new PublicFormFieldDto(f.Id, f.Label, (int)f.Type, f.Required, SplitOptions(f.Options)))
            .ToListAsync(ct);

    public async Task<FormFieldDto> CreateAsync(Guid companyId, CreateFormFieldRequest request, CancellationToken ct = default)
    {
        var actorId = await EnsureCompanyAdminAsync(companyId, ct);
        var nextOrder = (await db.FormFields.IgnoreQueryFilters()
            .Where(f => f.CompanyId == companyId && f.DeletedAt == null)
            .MaxAsync(f => (int?)f.SortOrder, ct) ?? -1) + 1;

        var field = new FormField(companyId, request.Label, (FormFieldType)request.Type, request.Required, nextOrder, request.Options);
        db.FormFields.Add(field);
        db.AuditLogs.Add(new AuditLog(actorId, "formfield.create", $"{companyId}:{request.Label}"));
        await db.SaveChangesAsync(ct);
        return new FormFieldDto(field.Id, field.Label, (int)field.Type, field.Required, field.SortOrder, field.Options, field.IsActive);
    }

    public async Task UpdateAsync(Guid fieldId, UpdateFormFieldRequest request, CancellationToken ct = default)
    {
        var field = await LoadAsync(fieldId, ct);
        var actorId = await EnsureCompanyAdminAsync(field.CompanyId, ct);
        field.Update(request.Label, (FormFieldType)request.Type, request.Required, request.SortOrder, request.Options);
        field.SetActive(request.IsActive);
        db.AuditLogs.Add(new AuditLog(actorId, "formfield.update", fieldId.ToString()));
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid fieldId, CancellationToken ct = default)
    {
        var field = await LoadAsync(fieldId, ct);
        var actorId = await EnsureCompanyAdminAsync(field.CompanyId, ct);
        db.FormFields.Remove(field); // interceptor soft-deletes
        db.AuditLogs.Add(new AuditLog(actorId, "formfield.delete", fieldId.ToString()));
        await db.SaveChangesAsync(ct);
    }

    private async Task<FormField> LoadAsync(Guid fieldId, CancellationToken ct) =>
        await db.FormFields.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == fieldId && f.DeletedAt == null, ct)
        ?? throw new NotFoundException("formfield.not_found", "Form field not found.");

    // Company admin or super admin. Returns the acting user's id (for audit).
    private async Task<Guid> EnsureCompanyAdminAsync(Guid companyId, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("auth.required", "Authentication required.");
        if (!currentUser.IsSuperAdmin &&
            !await db.Memberships.IgnoreQueryFilters()
                .AnyAsync(m => m.UserId == userId && m.CompanyId == companyId && m.Role == RoleType.Admin && m.DeletedAt == null, ct))
            throw new ForbiddenException("formfield.forbidden", "Only the company admin or a super admin can manage form fields.");
        return userId;
    }

    public static IReadOnlyList<string> SplitOptions(string? options) =>
        string.IsNullOrWhiteSpace(options)
            ? []
            : options.Split('\n').Select(o => o.Trim()).Where(o => o.Length > 0).ToList();
}
