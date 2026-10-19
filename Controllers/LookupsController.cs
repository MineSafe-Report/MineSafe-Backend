using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Small read-only lists for the app's buttons and filters.
[Route("api")]
[Authorize]
public class LookupsController : ApiControllerBase
{
    private readonly ILookupService _lookups;

    public LookupsController(ILookupService lookups)
    {
        _lookups = lookups;
    }

    // GET /api/incident-types   (the Hazard / Incident / Near Miss buttons, with their IDs)
    [HttpGet("incident-types")]
    public async Task<IActionResult> IncidentTypes() => Ok(await _lookups.GetIncidentTypesAsync());

    // GET /api/report-statuses  (for status filters)
    [HttpGet("report-statuses")]
    public async Task<IActionResult> ReportStatuses() => Ok(await _lookups.GetReportStatusesAsync());
}
