using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmKanban.Application.Common;

/// <summary>
/// Resolves a company from its public slug for the anonymous/customer entry points (public form,
/// company sign-in page). Runs unauthenticated, so it reads with IgnoreQueryFilters and refuses
/// archived/inactive companies — one place, so every public entry point closes the same way.
/// </summary>
public static class CompanyLookup
{
    public static async Task<Company> OpenBySlugAsync(IAppDbContext db, string slug, CancellationToken ct)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var company = await db.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Slug == normalized, ct)
            ?? throw new NotFoundException("company.not_found", "No company matches this link.");
        if (company.IsArchived || !company.IsActive)
            throw new ConflictException("company.form_closed", "This company is no longer accepting requests.");
        return company;
    }
}
