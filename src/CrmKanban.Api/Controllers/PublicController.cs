using CrmKanban.Application.PublicForm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrmKanban.Api.Controllers;

/// <summary>Anonymous portal endpoints that aren't tied to one company's slug (spec §10). The company
/// list feeds the register/new-message picker; only public fields are returned. Rate-limited.</summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public-form")]
[Route("api/public")]
public sealed class PublicController(PublicFormService publicForm) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<PublicCompanyDto>>> Companies(CancellationToken ct) =>
        Ok(await publicForm.ListOpenCompaniesAsync(ct));
}
