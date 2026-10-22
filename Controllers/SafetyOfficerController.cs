using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Endpoints under /api/safety-officer/incidents, used by the mobile app.
// A read-only view of every incident in the mine that is pending or approved (denied incidents are left out).
// The web dashboard uses the general /api/reports endpoints instead, where a Safety Officer sees every report.
[Route("api/safety-officer/incidents")]
[Authorize(Roles = RoleNames.SafetyOfficer)]
public class SafetyOfficerController : ApiControllerBase
{
    private readonly IReportService _reports;
    private readonly IAccessService _access;

    public SafetyOfficerController(IReportService reports, IAccessService access)
    {
        _reports = reports;
        _access = access;
    }

    // GET /api/safety-officer/incidents?status=APPROVED&locationId=2
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReportListFilter filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        filter.IncidentsOnly = true;
        return Ok(await _reports.ListAsync(user, filter));
    }

    // GET /api/safety-officer/incidents/5
    // Returns { "report": { ..., "dashboardUrl": "..." } }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetByIdAsync(id, user);
        if (!result.Success)
            return ErrorResponse(result);

        // This route only returns incidents that are pending or approved, like the list above.
        if (result.Value!.TypeCode != "INCIDENT" || result.Value.StatusCode == "DENIED")
            return NotFound(new { code = "NOT_FOUND", message = $"Incident {id} was not found." });

        return Ok(new ReportEnvelope { Report = result.Value });
    }
}
