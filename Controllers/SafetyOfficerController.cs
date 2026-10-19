using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Handles /api/safety-officer/incidents : read-only view of every Incident in the mine
// (submitted and approved; denied incidents are left out).
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

    // GET /api/safety-officer/incidents/5  ->  { "report": { ..., "dashboardUrl": "..." } }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _reports.GetByIdAsync(id, user);
        return result.Success ? Ok(new ReportEnvelope { Report = result.Value! }) : ErrorResponse(result);
    }
}
