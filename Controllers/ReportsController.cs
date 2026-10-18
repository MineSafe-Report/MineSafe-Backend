using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Handles /api/reports : the main feature of the app.
// Same receptionist pattern as LocationsController: work out who's asking, hand the request
// to ReportService, turn the result into a status code. The rules live in the service.
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

    // GET /api/reports?status=Approved&locationId=2&search=cable&page=1&pageSize=20
    // Everything you're allowed to see (Worker: own, Supervisor: your locations, Officer/Admin: all).
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        filter.MineOnly = false;
        filter.ReviewQueue = false;
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/reports/my         (the "My Reports" screen)
    [HttpGet("my")]
    public async Task<IActionResult> Mine([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        filter.MineOnly = true;
        filter.ReviewQueue = false;
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/reports/review-queue   ("Reports Waiting For Review": Pending only, urgent first)
    [HttpGet("review-queue")]
    [Authorize(Roles = RoleNames.Supervisor + "," + RoleNames.SafetyOfficer)]
    public async Task<IActionResult> ReviewQueue([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        filter.MineOnly = false;
        filter.ReviewQueue = true;
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/reports/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetByIdAsync(id, user);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // PUT /api/reports/5      (fix a report: own + Pending Review, or any as Safety Officer)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateReportRequest request)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.UpdateAsync(id, request, user);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // DELETE /api/reports/5   (withdraw = soft delete, same rules as editing)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Withdraw(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.WithdrawAsync(id, user);
        return result.Success ? NoContent() : ErrorResponse(result);
    }

    // POST /api/reports/5/reviews     body: { "decision": "APPROVED" }  or  { "decision": "DENIED", "comment": "why" }
    [HttpPost("{id:int}/reviews")]
    [Authorize(Roles = RoleNames.Supervisor + "," + RoleNames.SafetyOfficer)]
    public async Task<IActionResult> Review(int id, CreateReviewRequest request)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.ReviewAsync(id, request, user);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // GET /api/reports/5/audit        (the Safety Officer's "View Audit Log")
    [HttpGet("{id:int}/audit")]
    [Authorize(Roles = RoleNames.SafetyOfficer + "," + RoleNames.Administrator)]
    public async Task<IActionResult> Audit(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetAuditAsync(id, user);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }
}
