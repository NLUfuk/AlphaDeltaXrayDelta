using CrmKanban.Application.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>Per-company configurable public-form fields (spec §4.6). Admin/super-admin gate in the service.</summary>
[ApiController]
[Authorize]
[Route("api/companies/{companyId:guid}/form-fields")]
public sealed class FormFieldsController(FormFieldService fields) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormFieldDto>>> List(Guid companyId, CancellationToken ct) =>
        Ok(await fields.ListForCompanyAsync(companyId, ct));

    [HttpPost]
    public async Task<ActionResult<FormFieldDto>> Create(Guid companyId, CreateFormFieldRequest request, CancellationToken ct) =>
        Ok(await fields.CreateAsync(companyId, request, ct));

    [HttpPut("{fieldId:guid}")]
    public async Task<IActionResult> Update(Guid companyId, Guid fieldId, UpdateFormFieldRequest request, CancellationToken ct)
    {
        await fields.UpdateAsync(fieldId, request, ct);
        return NoContent();
    }

    [HttpDelete("{fieldId:guid}")]
    public async Task<IActionResult> Delete(Guid companyId, Guid fieldId, CancellationToken ct)
    {
        await fields.DeleteAsync(fieldId, ct);
        return NoContent();
    }
}
