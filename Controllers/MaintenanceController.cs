using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// One-off clean-up tasks. Administrators only.
[Route("api/admin/maintenance")]
[Authorize(Roles = RoleNames.Administrator)]
public class MaintenanceController : ApiControllerBase
{
    private readonly IReportService _reports;

    public MaintenanceController(IReportService reports)
    {
        _reports = reports;
    }

    // POST /api/admin/maintenance/stuck-supervisor-reports
    // { "dryRun": true }                                     lists what would happen, changes nothing
    // { "dryRun": false }                                    approves the reports whose evidence is complete
    // { "dryRun": false, "confirmLegacyReportIds": [4, 7] }  also approves these manually checked older reports
    [HttpPost("stuck-supervisor-reports")]
    public async Task<IActionResult> StuckSupervisorReports(StuckReportsRequest request)
    {
        return Ok(await _reports.ResolveStuckSupervisorReportsAsync(request, CurrentAccountId));
    }
}
