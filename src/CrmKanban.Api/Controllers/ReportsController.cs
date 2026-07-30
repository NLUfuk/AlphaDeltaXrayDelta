using System.Text;
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

    [HttpGet("company/{companyId:guid}/export.csv")]
    public async Task<IActionResult> CompanyExport(Guid companyId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Csv(await reports.CompanyExportCsvAsync(companyId, from, to, ct), $"report-{companyId}.csv");

    [HttpGet("global/export.csv")]
    public async Task<IActionResult> GlobalExport([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Csv(await reports.GlobalExportCsvAsync(from, to, ct), "report-global.csv");

    private FileContentResult Csv(string content, string fileName) =>
        File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray(), "text/csv", fileName);
}
