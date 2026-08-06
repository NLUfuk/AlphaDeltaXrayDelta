using CrmKanban.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>
/// Ticket reports (spec §15): dashboard metrics + CSV export. The service enforces scope — a company
/// report needs report.company over that company; the global report is super-admin only.
/// </summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("company/{companyId:guid}")]
    public async Task<ActionResult<TicketReport>> Company(Guid companyId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await reports.CompanyReportAsync(companyId, from, to, ct));

    [HttpGet("global")]
    public async Task<ActionResult<TicketReport>> Global([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await reports.GlobalReportAsync(from, to, ct));

    [HttpGet("company/{companyId:guid}/export.pdf")]
    public async Task<IActionResult> CompanyExport(Guid companyId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Pdf(await reports.ExportPdfAsync(companyId, from, to, ct), $"rapor-{companyId:N}.pdf");

    [HttpGet("global/export.pdf")]
    public async Task<IActionResult> GlobalExport([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Pdf(await reports.ExportPdfAsync(null, from, to, ct), "rapor-tum-sirketler.pdf");

    private FileContentResult Pdf(byte[] content, string fileName) => File(content, "application/pdf", fileName);
}
