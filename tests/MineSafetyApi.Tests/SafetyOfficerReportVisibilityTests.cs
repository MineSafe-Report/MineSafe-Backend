using MineSafety.Api.Dtos;
using MineSafety.Api.Services;
using Xunit;

namespace MineSafety.Api.Tests;

// Safety Officers see every active report on the web dashboard (all types and statuses, read-only),
// while the mobile incident list still only shows incidents that are pending or approved.
public class SafetyOfficerReportVisibilityTests
{
    [Fact]
    public async Task Safety_officer_can_list_and_open_all_report_types_including_denied_reports()
    {
        using var world = new TestWorld();
        var worker = world.User("W1001");
        var officer = world.User("O3001");
        var supervisor = world.User("S2001");
        var locationId = world.LocationId("T-7");

        async Task<int> Submit(string typeCode)
        {
            var result = await world.Reports.SubmitAsync(new CreateReportRequest
            {
                ClientReportId = Guid.NewGuid(),
                TypeCode = typeCode,
                LocationId = locationId,
                Description = "Safety officer visibility test",
                OccurredAt = DateTime.UtcNow.AddMinutes(-5)
            }, worker);
            Assert.True(result.Success);
            return result.Value!.Report.ReportId;
        }

        var incidentId = await Submit("INCIDENT");
        var hazardId = await Submit("HAZARD");
        var nearMissId = await Submit("NEAR_MISS");
        var denied = await world.Reports.ReviewAsync(hazardId,
            new CreateReviewRequest { Decision = "DENY", Reason = "Needs correction" }, supervisor);
        Assert.True(denied.Success);

        // The web dashboard list: every report, including the denied hazard.
        var all = await world.Reports.ListAsync(officer, new ReportListFilter { PageSize = 100 });
        Assert.Equal(3, all.Total);
        Assert.Contains(all.Items, report => report.ReportId == incidentId);
        Assert.Contains(all.Items, report => report.ReportId == hazardId && report.StatusCode == "DENIED");
        Assert.Contains(all.Items, report => report.ReportId == nearMissId);

        // Filtering by type, opening a report and viewing its audit history.
        var hazards = await world.Reports.ListAsync(officer, new ReportListFilter { TypeCode = "HAZARD" });
        Assert.Single(hazards.Items);
        Assert.Equal(hazardId, hazards.Items[0].ReportId);
        Assert.True((await world.Reports.GetByIdAsync(hazardId, officer)).Success);
        Assert.True((await world.Reports.GetAuditAsync(hazardId, officer)).Success);

        // The mobile incident list is unchanged: incidents only.
        var mobileIncidents = await world.Reports.ListAsync(officer, new ReportListFilter { IncidentsOnly = true });
        Assert.Single(mobileIncidents.Items);
        Assert.Equal(incidentId, mobileIncidents.Items[0].ReportId);
    }
}
