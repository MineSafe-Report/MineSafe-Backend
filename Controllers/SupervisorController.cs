using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Handles /api/supervisor/reports : the supervisor's screens.
// Only reports at the supervisor's assigned locations, never their own.
[Route("api/supervisor/reports")]
[Authorize(Roles = RoleNames.Supervisor)]
public class SupervisorController : ApiControllerBase
{
    private readonly IReportService _reports;
    private readonly IAccessService _access;

    public SupervisorController(IReportService reports, IAccessService access)
    {
        _reports = reports;
        _access = access;
    }

    // GET /api/supervisor/reports?status=PENDING_REVIEW   ("Reports Waiting For Review", urgent first)
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        filter.AssignedLocationsOnly = true;
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/supervisor/reports/5   ->  { "report": { ... } }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetByIdAsync(id, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }

    // POST /api/supervisor/reports/5/review
    // body: { "decision": "APPROVE" }   or   { "decision": "DENY", "reason": "why" }
    // 200 { "report": ... } | 400 REASON_REQUIRED | 404 | 409 ALREADY_REVIEWED
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, CreateReviewRequest request)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.ReviewAsync(id, request, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }
}
