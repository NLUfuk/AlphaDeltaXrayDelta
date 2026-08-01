using CrmKanban.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>Company lifecycle + member listing (spec §8/§9/§18.8). Scope/ownership enforced in the service.</summary>
[ApiController]
[Authorize]
[Route("api/companies")]
public sealed class CompaniesController(CompanyService companies) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> List(CancellationToken ct) =>
        Ok(await companies.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<CompanyDto>> Create(CreateCompanyRequest request, CancellationToken ct) =>
        Ok(await companies.CreateAsync(request, ct));

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await companies.ArchiveAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> Members(Guid id, CancellationToken ct) =>
        Ok(await companies.ListMembersAsync(id, ct));

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await companies.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }
}
