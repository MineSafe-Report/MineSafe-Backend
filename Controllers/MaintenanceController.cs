using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Administrator-only, one-time clean-up tasks.
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
    // body: { "dryRun": true }                                    -> list only, changes nothing
    //       { "dryRun": false }                                   -> approve the ones that are complete
    //       { "dryRun": false, "confirmLegacyReportIds": [4, 7] } -> also approve these checked older reports
    [HttpPost("stuck-supervisor-reports")]
    public async Task<IActionResult> StuckSupervisorReports(StuckReportsRequest request)
    {
        return Ok(await _reports.ResolveStuckSupervisorReportsAsync(request, CurrentAccountId));
    }
}
