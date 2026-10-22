using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;
using Xunit;

namespace MineSafety.Api.Tests;

// Supervisors can't review their own reports, so their reports are approved automatically
// once all of the expected evidence has arrived. These tests cover that behaviour.
public class SupervisorAutoApprovalTests
{
    private static CreateReportRequest Report(
        string typeCode, int locationId, int images = 0, int voiceNotes = 0, Guid? clientId = null) => new()
        {
            ClientReportId = clientId ?? Guid.NewGuid(),
            TypeCode = typeCode,
            LocationId = locationId,
            Description = "Loose rock above the walkway.",
            OccurredAt = DateTime.UtcNow.AddMinutes(-30),
            ExpectedImageCount = images,
            ExpectedVoiceNoteCount = voiceNotes
        };

    private static UploadAttachmentRequest Photo(string clientId, byte seed) => new()
    {
        File = TestWorld.File("image/jpeg", new byte[] { 0xFF, 0xD8, seed, 1, 2, 3 }),
        AttachmentType = "PHOTO",
        ClientAttachmentId = clientId
    };

    private static UploadAttachmentRequest Voice(string clientId) => new()
    {
        File = TestWorld.File("audio/mp4", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 }),
        AttachmentType = "VOICE_NOTE",
        ClientAttachmentId = clientId
    };

    private static int Count(TestWorld w, int reportId, string action) =>
        w.Db.ReportAuditLog.Count(a => a.ReportId == reportId && a.Action == action);

    // How many notifications of this type the Safety Officer (O3001) received for this report.
    private static int OfficerNotifications(TestWorld w, int reportId, string type)
    {
        var officerId = w.User("O3001").AccountId;
        return w.Db.Notifications.Count(n => n.ReportId == reportId && n.NotificationType == type
                                             && n.RecipientAccountId == officerId);
    }

    [Fact]
    public async Task Supervisor_report_with_no_attachments_is_approved_on_creation()
    {
        using var w = new TestWorld();
        var supervisor = w.User("S2001");

        var result = await w.Reports.SubmitAsync(Report("INCIDENT", w.LocationId("T-7")), supervisor);

        Assert.True(result.Success);
        var report = result.Value!.Report;
        Assert.Equal("APPROVED", report.StatusCode);
        Assert.True(report.AutoApproved);
        Assert.True(report.EvidenceComplete);
        Assert.Empty(report.Reviews); // no supervisor reviewed it
        Assert.Equal(0, w.Db.ReportReviews.Count(r => r.ReportId == report.ReportId));
        Assert.Equal(1, Count(w, report.ReportId, ReportService.AutoApprovedAction));
        Assert.Equal(0, Count(w, report.ReportId, ReviewDecisions.Approved));
        Assert.Equal(1, OfficerNotifications(w, report.ReportId, NotificationTypes.IncidentAlert));    // sent when submitted
        Assert.Equal(1, OfficerNotifications(w, report.ReportId, NotificationTypes.IncidentApproved)); // sent when approved
    }

    [Fact]
    public async Task Supervisor_report_with_a_voice_note_is_approved_when_the_voice_note_arrives()
    {
        using var w = new TestWorld();
        var supervisor = w.User("S2001");

        var created = await w.Reports.SubmitAsync(Report("HAZARD", w.LocationId("T-7"), voiceNotes: 1), supervisor);
        var reportId = created.Value!.Report.ReportId;
        Assert.Equal("PENDING_REVIEW", created.Value.Report.StatusCode);
        Assert.False(created.Value.Report.EvidenceComplete);

        var upload = await w.Attachments.UploadAsync(reportId, Voice("voice-1"), supervisor);

        Assert.True(upload.Success);
        Assert.Equal("APPROVED", upload.Value!.ReportStatusCode);
        Assert.True(upload.Value.EvidenceComplete);
        var mine = await w.Reports.ListAsync(supervisor, new ReportListFilter { MineOnly = true });
        Assert.Equal("APPROVED", mine.Items.Single().StatusCode); // what My Reports shows
        Assert.Equal(1, Count(w, reportId, ReportService.AutoApprovedAction));
        Assert.Equal(0, OfficerNotifications(w, reportId, NotificationTypes.IncidentApproved)); // a hazard, not an incident
    }

    [Fact]
    public async Task Incomplete_or_failed_upload_keeps_it_pending_and_blocks_manual_approval()
    {
        using var w = new TestWorld();
        var supervisor = w.User("S2001");
        var otherSupervisor = w.User("S2003");

        var created = await w.Reports.SubmitAsync(Report("INCIDENT", w.LocationId("T-7"), images: 2), supervisor);
        var reportId = created.Value!.Report.ReportId;

        var first = await w.Attachments.UploadAsync(reportId, Photo("image-1", 1), supervisor);
        Assert.Equal("PENDING_REVIEW", first.Value!.ReportStatusCode); // 1 of 2 photos

        // An upload with the wrong file type is rejected and doesn't count.
        var failed = await w.Attachments.UploadAsync(reportId, new UploadAttachmentRequest
        {
            File = TestWorld.File("application/pdf", new byte[] { 1, 2, 3 }),
            AttachmentType = "PHOTO",
            ClientAttachmentId = "image-2"
        }, supervisor);
        Assert.False(failed.Success);
        Assert.Equal("UNSUPPORTED_FILE_TYPE", failed.Code);

        var detail = await w.Reports.GetByIdAsync(reportId, otherSupervisor);
        Assert.Equal("PENDING_REVIEW", detail.Value!.StatusCode);
        Assert.False(detail.Value.EvidenceComplete);

        // Another supervisor can't approve it while evidence is missing...
        var approve = await w.Reports.ReviewAsync(reportId, new CreateReviewRequest { Decision = "APPROVE" }, otherSupervisor);
        Assert.False(approve.Success);
        Assert.Equal("EVIDENCE_INCOMPLETE", approve.Code);

        // ...and the reporter can't review their own report at all.
        var own = await w.Reports.ReviewAsync(reportId, new CreateReviewRequest { Decision = "APPROVE" }, supervisor);
        Assert.Equal("OWN_REPORT", own.Code);

        Assert.Equal(0, Count(w, reportId, ReportService.AutoApprovedAction));
        Assert.Equal(0, OfficerNotifications(w, reportId, NotificationTypes.IncidentApproved));

        // The second photo arrives (the failed one, retried as a real image), so the report is approved.
        var second = await w.Attachments.UploadAsync(reportId, Photo("image-2", 2), supervisor);
        Assert.Equal("APPROVED", second.Value!.ReportStatusCode);
    }

    [Fact]
    public async Task Retries_and_duplicate_uploads_never_approve_or_notify_twice()
    {
        using var w = new TestWorld();
        var supervisor = w.User("S2001");
        var clientReportId = Guid.NewGuid();
        var request = Report("INCIDENT", w.LocationId("T-7"), images: 1, clientId: clientReportId);

        var first = await w.Reports.SubmitAsync(request, supervisor);
        var retry = await w.Reports.SubmitAsync(request, supervisor); // the phone retries
        Assert.False(first.Value!.AlreadyExisted);
        Assert.True(retry.Value!.AlreadyExisted);
        var reportId = first.Value.Report.ReportId;
        Assert.Equal(reportId, retry.Value.Report.ReportId);
        Assert.Equal(1, w.Db.Reports.Count(r => r.ClientReportId == clientReportId));

        var upload1 = await w.Attachments.UploadAsync(reportId, Photo("image-7", 7), supervisor);
        var upload2 = await w.Attachments.UploadAsync(reportId, Photo("image-7", 7), supervisor); // the same upload again
        Assert.False(upload1.Value!.AlreadyExisted);
        Assert.True(upload2.Value!.AlreadyExisted);
        Assert.Equal(upload1.Value.Attachment.AttachmentId, upload2.Value.Attachment.AttachmentId);
        Assert.Equal(1, w.Db.ReportAttachments.Count(a => a.ReportId == reportId));

        // Running the checks again after approval must not change anything.
        await w.Reports.TryAutoApproveAsync(reportId, supervisor);
        var retryAfter = await w.Reports.SubmitAsync(request, supervisor);
        Assert.Equal("APPROVED", retryAfter.Value!.Report.StatusCode);

        Assert.Equal(1, Count(w, reportId, ReportService.AutoApprovedAction));
        Assert.Equal(1, OfficerNotifications(w, reportId, NotificationTypes.IncidentApproved));
        Assert.Equal(1, OfficerNotifications(w, reportId, NotificationTypes.IncidentAlert));
    }

    [Fact]
    public async Task Worker_report_still_needs_a_supervisor_review()
    {
        using var w = new TestWorld();
        var worker = w.User("W1001");
        var supervisor = w.User("S2001");

        var created = await w.Reports.SubmitAsync(Report("NEAR_MISS", w.LocationId("T-7")), worker);
        var reportId = created.Value!.Report.ReportId;
        Assert.Equal("PENDING_REVIEW", created.Value.Report.StatusCode); // not approved automatically
        Assert.True(created.Value.Report.EvidenceComplete);

        await w.Reports.TryAutoApproveAsync(reportId, worker); // should do nothing for a worker
        Assert.Equal(0, Count(w, reportId, ReportService.AutoApprovedAction));

        var reviewed = await w.Reports.ReviewAsync(reportId, new CreateReviewRequest { Decision = "APPROVE" }, supervisor);
        Assert.True(reviewed.Success);
        Assert.Equal("APPROVED", reviewed.Value!.StatusCode);
        Assert.False(reviewed.Value.AutoApproved); // approved through a real review
        Assert.Single(reviewed.Value.Reviews);
    }

    [Fact]
    public async Task Stuck_legacy_supervisor_reports_need_explicit_confirmation()
    {
        using var w = new TestWorld();
        var supervisor = w.User("S2001");
        var admin = w.User("A9001");

        // Simulate a report created before evidence tracking, with no expected counts stored.
        var created = await w.Reports.SubmitAsync(Report("HAZARD", w.LocationId("T-7"), images: 1), supervisor);
        var reportId = created.Value!.Report.ReportId;
        var row = await w.Db.Reports.SingleAsync(r => r.ReportId == reportId);
        row.ExpectedImageCount = null;
        row.ExpectedVoiceNoteCount = null;
        await w.Db.SaveChangesAsync();

        var dryRun = await w.Reports.ResolveStuckSupervisorReportsAsync(new StuckReportsRequest { DryRun = true }, admin.AccountId);
        Assert.Equal("NEEDS_CONFIRMATION", dryRun.Single(r => r.ReportId == reportId).Action);

        var notConfirmed = await w.Reports.ResolveStuckSupervisorReportsAsync(new StuckReportsRequest { DryRun = false }, admin.AccountId);
        Assert.Equal("NEEDS_CONFIRMATION", notConfirmed.Single(r => r.ReportId == reportId).Action);
        Assert.Equal("PENDING_REVIEW", (await w.Reports.GetByIdAsync(reportId, supervisor)).Value!.StatusCode);

        var confirmed = await w.Reports.ResolveStuckSupervisorReportsAsync(
            new StuckReportsRequest { DryRun = false, ConfirmLegacyReportIds = { reportId } }, admin.AccountId);
        Assert.Equal("APPROVED", confirmed.Single(r => r.ReportId == reportId).Action);
        Assert.Equal(1, w.Db.ReportAuditLog.Count(a => a.ReportId == reportId
                                                        && a.Action == ReportService.LegacyAutoApprovedAction
                                                        && a.ActorAccountId == admin.AccountId));
    }
}
