using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Endpoints under /api/reports: submitting reports and viewing your own.
// Supervisor endpoints are in SupervisorController and Safety Officer endpoints in SafetyOfficerController.
[Route("api/reports")]
[Authorize]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reports;
    private readonly IAccessService _access;

    public ReportsController(IReportService reports, IAccessService access)
    {
        _reports = reports;
        _access = access;
    }

    // POST /api/reports
    // Called when a report is submitted, or when the phone syncs after being offline.
    // 201 = new report saved, 200 = this report was already received (a retry).
    [HttpPost]
    [Authorize(Roles = RoleNames.Worker + "," + RoleNames.Supervisor)] // either role may submit
    public async Task<IActionResult> Submit(CreateReportRequest request)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.SubmitAsync(request, user);
        if (!result.Success)
            return ErrorResponse(result);

        if (result.Value!.AlreadyExisted)
            return Ok(result.Value);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Report.ReportId }, result.Value);
    }

    // GET /api/reports/mine?status=PENDING_REVIEW&page=1&pageSize=20
    // The "My Reports" screen. /api/reports/my works as well.
    [HttpGet("mine")]
    [HttpGet("my")]
    public async Task<IActionResult> Mine([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        filter.MineOnly = true;
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/reports
    // Every report you are allowed to see, with optional filters.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/reports/5
    // Returns { "report": { ... } }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetByIdAsync(id, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }

    // PUT /api/reports/5
    // The reporter can edit their own report while it is still Pending Review.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateReportRequest request)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.UpdateAsync(id, request, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }

    // DELETE /api/reports/5
    // Withdraws the report (soft delete). Same rule as editing.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Withdraw(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.WithdrawAsync(id, user);
        return result.Success ? NoContent() : ErrorResponse(result);
    }

    // GET /api/reports/5/audit
    // The Safety Officer's audit log view.
    [HttpGet("{id:int}/audit")]
    [Authorize(Roles = RoleNames.SafetyOfficer)]
    public async Task<IActionResult> Audit(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetAuditAsync(id, user);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }
}
