using CrmKanban.Application.Companies;
using CrmKanban.Application.PublicForm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>Company lifecycle + member listing (spec §8/§9/§18.8). Scope/ownership enforced in the service.</summary>
[ApiController]
[Authorize]
[Route("api/companies")]
public sealed class CompaniesController(CompanyService companies, CustomerInviteService customerInvites) : ControllerBase
{
    /// <summary>Mint a per-customer sign-in link (<c>/c/{slug}?davet=…</c>) whose holder's first ticket
    /// skips the moderation queue (Faz 35). Requires user.invite on this company. The raw token is
    /// returned on purpose — staff paste the link into WhatsApp; it is not a login credential.</summary>
    [HttpPost("{id:guid}/customer-invites")]
    public async Task<ActionResult<CustomerInviteResult>> CreateCustomerInvite(
        Guid id, CustomerInviteRequest request, CancellationToken ct) =>
        Ok(await customerInvites.CreateAsync(id, request, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> List(CancellationToken ct) =>
        Ok(await companies.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<CompanyDto>> Create(CreateCompanyRequest request, CancellationToken ct) =>
        Ok(await companies.CreateAsync(request, ct));

    /// <summary>Edit name + contact card. The slug is not editable — customer links point at it.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> Update(Guid id, UpdateCompanyRequest request, CancellationToken ct) =>
        Ok(await companies.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await companies.ArchiveAsync(id, ct);
        return NoContent();
    }

    /// <summary>Delete a company. Body carries the typed company name as the second confirmation.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteCompanyRequest request, CancellationToken ct)
    {
        await companies.DeleteAsync(id, request, ct);
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
