using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Handles /api/reports : submitting reports and the worker's own views.
// Same receptionist pattern as LocationsController: work out who's asking, hand the request
// to ReportService, turn the result into a status code. The rules live in the service.
// Supervisor routes: SupervisorController. Safety Officer routes: SafetyOfficerController.
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

    // POST /api/reports        (the "Submit Report" button, or the phone syncing after being offline)
    // 201 = new report saved.  200 = we already had this one (a retry), here it is again.
    [HttpPost]
    [Authorize(Roles = RoleNames.Worker + "," + RoleNames.Supervisor)]   // comma = either role
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

    // GET /api/reports/mine?status=PENDING_REVIEW&page=1&pageSize=20     ("My Reports")
    // (/api/reports/my also works)
    [HttpGet("mine")]
    [HttpGet("my")]
    public async Task<IActionResult> Mine([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        filter.MineOnly = true;
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/reports  : every report you're allowed to see, with filters
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/reports/5   ->  { "report": { ... } }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetByIdAsync(id, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }

    // PUT /api/reports/5      (fix your own report while it's still Pending Review)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateReportRequest request)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.UpdateAsync(id, request, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }

    // DELETE /api/reports/5   (withdraw = soft delete, same rule as editing)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Withdraw(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.WithdrawAsync(id, user);
        return result.Success ? NoContent() : ErrorResponse(result);
    }

    // GET /api/reports/5/audit        (the Safety Officer's "View Audit Log")
    [HttpGet("{id:int}/audit")]
    [Authorize(Roles = RoleNames.SafetyOfficer)]
    public async Task<IActionResult> Audit(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetAuditAsync(id, user);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }
}
