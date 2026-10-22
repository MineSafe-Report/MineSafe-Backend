using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Endpoints under /api/supervisor/reports, used by the supervisor screens.
// Supervisors only see reports at their assigned locations, and never their own.
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

    // GET /api/supervisor/reports?status=PENDING_REVIEW
    // The review queue, with urgent reports first.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        filter.AssignedLocationsOnly = true;
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/supervisor/reports/5
    // Returns { "report": { ... } }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetByIdAsync(id, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }

    // POST /api/supervisor/reports/5/review
    // Body: { "decision": "APPROVE" } or { "decision": "DENY", "reason": "..." }
    // Returns 200 with the report, 400 REASON_REQUIRED, 404, or 409 ALREADY_REVIEWED.
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, CreateReviewRequest request)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.ReviewAsync(id, request, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }
}
