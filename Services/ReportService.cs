using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Dtos;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Services;

public interface IReportService
{
    Task<ServiceResult<SubmitReportResponse>> SubmitAsync(CreateReportRequest request, CurrentUser user);
    Task<PagedResult<ReportSummaryDto>> ListAsync(CurrentUser user, ReportListFilter filter);
    Task<ServiceResult<ReportDetailDto>> GetByIdAsync(int id, CurrentUser user);
    Task<ServiceResult<ReportDetailDto>> UpdateAsync(int id, UpdateReportRequest request, CurrentUser user);
    Task<ServiceResult<bool>> WithdrawAsync(int id, CurrentUser user);
    Task<ServiceResult<ReportDetailDto>> ReviewAsync(int id, CreateReviewRequest request, CurrentUser user);
    Task<ServiceResult<List<AuditEntryDto>>> GetAuditAsync(int id, CurrentUser user);

    // Shared with AttachmentService so both use the same rule for who may see a report.
    IQueryable<Report> VisibleTo(CurrentUser user);
    void AddAudit(int reportId, int actorId, string action, object? before, object? after);

    // Called after every evidence upload. Approves a Supervisor's own report once all of its
    // expected photos and voice notes have arrived. Safe to call any number of times.
    Task TryAutoApproveAsync(int reportId, CurrentUser reporter);

    // The report's status code and whether its evidence is complete (used in the upload response).
    Task<(string StatusCode, bool EvidenceComplete)> GetEvidenceStateAsync(int reportId);

    // One-off clean-up of Supervisor reports stuck in Pending Review (administrators only).
    Task<List<StuckReportDto>> ResolveStuckSupervisorReportsAsync(StuckReportsRequest request, int adminAccountId);
}

// Filters for the report lists. Status accepts a code (PENDING_REVIEW) or a name (Pending Review).
public class ReportListFilter
{
    public string? Status { get; set; }
    public string? TypeCode { get; set; }
    public int? LocationId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // [BindNever] means these can't be set from the URL. The controller sets them for each route.
    [BindNever] public bool MineOnly { get; set; }              // only reports the user submitted
    [BindNever] public bool AssignedLocationsOnly { get; set; } // supervisor view: their locations, excluding their own reports
    [BindNever] public bool IncidentsOnly { get; set; }         // Safety Officer view
}

// The business rules for reports. Most methods follow the same steps:
// load the report, check the rules, make the change, save it and return a DTO.
public class ReportService : IReportService
{
    // Audit log actions for approvals that didn't come from a supervisor's review.
    public const string AutoApprovedAction = "AUTO_APPROVED";
    public const string LegacyAutoApprovedAction = "AUTO_APPROVED_BY_ADMIN_CHECK";

    private readonly MineSafetyDbContext _db;
    private readonly IConfiguration _config;

    public ReportService(MineSafetyDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // ---------- Visibility ----------

    // Which reports a user may see. Used by every read, edit, review and file download.
    //   Everyone:        their own reports
    //   Supervisor:      plus every report at their assigned locations
    //   Safety Officer:  plus every active report in the mine (read-only)
    //   Administrator:   no reports (they only manage accounts and locations)
    // Withdrawn reports (IsActive = false) are hidden from everyone.
    public IQueryable<Report> VisibleTo(CurrentUser user)
    {
        var isSupervisor = user.IsInRole(RoleNames.Supervisor);
        var isOfficer = user.IsInRole(RoleNames.SafetyOfficer);
        var supervisorSeesAll = isSupervisor && user.AllowedLocationIds == null;
        var locations = user.AllowedLocationIds ?? new List<int>();

        return _db.Reports.Where(r => r.IsActive && (
            r.ReporterAccountId == user.AccountId
            || (isSupervisor && (supervisorSeesAll || locations.Contains(r.LocationId)))
            || isOfficer));
    }

    private IQueryable<Report> WithDetails(IQueryable<Report> query) => query
        .Include(r => r.Reporter)
        .Include(r => r.IncidentType)
        .Include(r => r.Location)
        .Include(r => r.Status)
        .Include(r => r.Reviews).ThenInclude(rv => rv.Reviewer)
        .Include(r => r.Attachments);

    // ---------- Create (POST /api/reports) ----------

    public async Task<ServiceResult<SubmitReportResponse>> SubmitAsync(CreateReportRequest request, CurrentUser user)
    {
        if (request.ClientReportId == Guid.Empty)
            return ServiceResult<SubmitReportResponse>.BadRequest("clientReportId is required.", "CLIENT_ID_REQUIRED");

        var occurredAt = ToUtc(request.OccurredAt);
        if (occurredAt > DateTime.UtcNow.AddMinutes(5))
            return ServiceResult<SubmitReportResponse>.BadRequest("occurredAt can't be in the future.", "OCCURRED_IN_FUTURE");

        var incidentType = await ResolveTypeAsync(request.TypeCode, request.IncidentTypeId);
        if (incidentType == null)
            return ServiceResult<SubmitReportResponse>.BadRequest("Unknown report type. Use INCIDENT, HAZARD or NEAR_MISS.", "UNKNOWN_TYPE");

        // 1. Check whether a report with this ClientReportId was already received. This happens when
        //    the phone uploaded it but lost signal before the reply arrived, so it sends it again.
        //    The report must not be saved twice.
        var duplicate = await CheckForDuplicateAsync(request, incidentType.IncidentTypeId, occurredAt, user);
        if (duplicate != null)
            return duplicate;

        // 2. Validate the location.
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.LocationId == request.LocationId && l.IsActive);
        if (location == null || !user.CanSeeLocation(location.LocationId))
            return ServiceResult<SubmitReportResponse>.BadRequest("You can't report at this location.", "LOCATION_NOT_ALLOWED");

        var pendingStatusId = await GetStatusIdAsync(ReportStatusNames.PendingReview);
        var now = DateTime.UtcNow;

        var report = new Report
        {
            ClientReportId = request.ClientReportId,
            // The reporter is always the logged-in user, never a value sent by the app.
            ReporterAccountId = user.AccountId,
            IncidentTypeId = incidentType.IncidentTypeId,
            LocationId = location.LocationId,
            StatusId = pendingStatusId,
            Description = (request.Description ?? "").Trim(),
            OccurredAt = occurredAt,
            // No count sent means nothing is expected. Stored so the evidence check still works after a restart.
            ExpectedImageCount = request.ExpectedImageCount ?? 0,
            ExpectedVoiceNoteCount = request.ExpectedVoiceNoteCount ?? 0,
            IsActive = true,
            CreatedAt = now,
            SubmittedAt = now,
            // ReportNumber must be unique and can't be empty, but the final number (MS-2026-00042)
            // uses the ReportID, which only exists after the INSERT. So a temporary number is saved
            // first (unique because ClientReportId is unique) and replaced straight after.
            ReportNumber = "TMP-" + request.ClientReportId.ToString("N")
        };

        // 3. Save the report, its number, the audit entry and the notifications in one transaction.
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            _db.Reports.Add(report);
            await _db.SaveChangesAsync(); // INSERT, SQL Server generates the ReportId

            report.ReportNumber = $"MS-{now:yyyy}-{report.ReportId:D5}";
            AddAudit(report.ReportId, user.AccountId, "CREATED", null, Snapshot(report));
            await AddNewReportNotificationsAsync(report, incidentType, location, user.AccountId);

            // A Supervisor can't review their own report, so it is approved automatically.
            // If no photos or voice notes are expected, that happens now, in the same transaction.
            // Otherwise it happens when the last file arrives (TryAutoApproveAsync, called by AttachmentService).
            if (IsSupervisor(user) && CanAutoApprove(report, receivedImages: 0, receivedVoiceNotes: 0))
                await ApplyAutoApprovalAsync(report, incidentType, location, user.AccountId, AutoApprovedAction,
                    "Submitted by a supervisor; approved automatically with no evidence outstanding.");

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Two copies of the same report arrived at the same moment and the other one was saved first.
            // The unique constraint on ClientReportId rejected this one, so treat it as a retry.
            _db.ChangeTracker.Clear();
            var retry = await CheckForDuplicateAsync(request, incidentType.IncidentTypeId, occurredAt, user);
            if (retry != null)
                return retry;
            throw;
        }

        var saved = await LoadDetailAsync(report.ReportId, user);
        return ServiceResult<SubmitReportResponse>.Ok(new SubmitReportResponse { AlreadyExisted = false, Report = saved! });
    }

    // Returns null if this isn't a duplicate, otherwise the response to send back.
    private async Task<ServiceResult<SubmitReportResponse>?> CheckForDuplicateAsync(
        CreateReportRequest request, int incidentTypeId, DateTime occurredAt, CurrentUser user)
    {
        var existing = await _db.Reports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ClientReportId == request.ClientReportId);
        if (existing == null)
            return null;

        // The same ID with a different reporter or different content isn't a retry, so it's rejected.
        var sameReport = existing.ReporterAccountId == user.AccountId
            && existing.IncidentTypeId == incidentTypeId
            && existing.LocationId == request.LocationId
            && existing.Description == (request.Description ?? "").Trim()
            && Math.Abs((existing.OccurredAt - occurredAt).TotalSeconds) < 1;

        if (!sameReport)
            return ServiceResult<SubmitReportResponse>.Conflict(
                "This clientReportId was already used for a different report. Generate a new ID for a new report.",
                "CLIENT_ID_CONFLICT");

        // A genuine retry: return the existing report unchanged.
        var details = await WithDetails(_db.Reports.AsNoTracking()).FirstAsync(r => r.ReportId == existing.ReportId);
        return ServiceResult<SubmitReportResponse>.Ok(new SubmitReportResponse
        {
            AlreadyExisted = true,
            Report = ToDetail(details, user)
        });
    }

    // Notifies every Supervisor assigned to the report's location, and for incidents every
    // Safety Officer as well. The reporter is never notified about their own report.
    private async Task AddNewReportNotificationsAsync(Report report, IncidentType type, Location location, int reporterId)
    {
        var urgent = type.TriggersImmediateAlert;

        var recipients = await _db.AccountLocations
            .Where(al => al.LocationId == location.LocationId && al.IsActive && al.Account.IsActive
                         && al.Account.AccountRoles.Any(ar => ar.IsActive && ar.Role.RoleName == RoleNames.Supervisor))
            .Select(al => al.AccountId)
            .ToListAsync();

        if (urgent)
            recipients.AddRange(await SafetyOfficerIdsAsync());

        var message = urgent
            ? $"URGENT: {type.TypeName} reported at {location.LocationName} ({report.ReportNumber})."
            : $"New {type.TypeName} report at {location.LocationName} ({report.ReportNumber}).";

        foreach (var accountId in recipients.Distinct().Where(id => id != reporterId))
            AddNotification(accountId, report.ReportId, urgent ? NotificationTypes.IncidentAlert : NotificationTypes.NewReport, message);
    }

    // ---------- Read ----------

    public async Task<PagedResult<ReportSummaryDto>> ListAsync(CurrentUser user, ReportListFilter filter)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = VisibleTo(user);

        if (filter.MineOnly)
            query = query.Where(r => r.ReporterAccountId == user.AccountId);

        if (filter.AssignedLocationsOnly)
        {
            var locations = user.AllowedLocationIds;
            query = query.Where(r => r.ReporterAccountId != user.AccountId); // supervisors can't review their own reports
            if (locations != null)
                query = query.Where(r => locations.Contains(r.LocationId));
        }

        if (filter.IncidentsOnly)
            query = query.Where(r => r.IncidentType.TriggersImmediateAlert && r.Status.StatusName != ReportStatusNames.Denied);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var statusName = await ResolveStatusNameAsync(filter.Status);
            query = query.Where(r => r.Status.StatusName == statusName);
        }

        if (!string.IsNullOrWhiteSpace(filter.TypeCode))
        {
            var type = await ResolveTypeAsync(filter.TypeCode, null);
            var typeId = type?.IncidentTypeId ?? -1;
            query = query.Where(r => r.IncidentTypeId == typeId);
        }

        if (filter.LocationId.HasValue)
            query = query.Where(r => r.LocationId == filter.LocationId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(r => r.ReportNumber.Contains(s) || r.Description.Contains(s));
        }

        var total = await query.CountAsync();

        // The pending review queue shows urgent reports first, then the oldest first.
        // Every other list shows the newest first.
        var pendingQueue = filter.AssignedLocationsOnly
            && (await ResolveStatusNameAsync(filter.Status ?? "")) == ReportStatusNames.PendingReview;
        query = pendingQueue
            ? query.OrderByDescending(r => r.IncidentType.TriggersImmediateAlert).ThenBy(r => r.SubmittedAt)
            : query.OrderByDescending(r => r.SubmittedAt);

        var reports = await WithDetails(query.AsNoTracking())
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ReportSummaryDto>
        {
            Items = reports.Select(r => (ReportSummaryDto)ToSummary(r)).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<ServiceResult<ReportDetailDto>> GetByIdAsync(int id, CurrentUser user)
    {
        // A report the user isn't allowed to see is reported as not found (404).
        var detail = await LoadDetailAsync(id, user);
        return detail == null
            ? ServiceResult<ReportDetailDto>.NotFound($"Report {id} was not found.")
            : ServiceResult<ReportDetailDto>.Ok(detail);
    }

    private async Task<ReportDetailDto?> LoadDetailAsync(int id, CurrentUser user)
    {
        var report = await WithDetails(VisibleTo(user).AsNoTracking()).FirstOrDefaultAsync(r => r.ReportId == id);
        return report == null ? null : ToDetail(report, user);
    }

    // ---------- Update (PUT /api/reports/5) ----------

    // Only the reporter, and only while the report is Pending Review.
    public async Task<ServiceResult<ReportDetailDto>> UpdateAsync(int id, UpdateReportRequest request, CurrentUser user)
    {
        var report = await WithDetails(VisibleTo(user)).FirstOrDefaultAsync(r => r.ReportId == id);
        if (report == null)
            return ServiceResult<ReportDetailDto>.NotFound($"Report {id} was not found.");

        if (!CanEdit(report, user))
            return ServiceResult<ReportDetailDto>.BadRequest(
                "You can only edit your own report while it is Pending Review.", "NOT_EDITABLE");

        var occurredAt = ToUtc(request.OccurredAt);
        if (occurredAt > DateTime.UtcNow.AddMinutes(5))
            return ServiceResult<ReportDetailDto>.BadRequest("occurredAt can't be in the future.", "OCCURRED_IN_FUTURE");

        var type = await ResolveTypeAsync(request.TypeCode, request.IncidentTypeId);
        if (type == null)
            return ServiceResult<ReportDetailDto>.BadRequest("Unknown report type. Use INCIDENT, HAZARD or NEAR_MISS.", "UNKNOWN_TYPE");

        if (!await _db.Locations.AnyAsync(l => l.LocationId == request.LocationId && l.IsActive)
            || !user.CanSeeLocation(request.LocationId))
            return ServiceResult<ReportDetailDto>.BadRequest("You can't use this location.", "LOCATION_NOT_ALLOWED");

        var before = Snapshot(report);

        report.IncidentTypeId = type.IncidentTypeId;
        report.LocationId = request.LocationId;
        report.Description = (request.Description ?? "").Trim();
        report.OccurredAt = occurredAt;
        report.UpdatedAt = DateTime.UtcNow;

        // The audit entry records what changed, the old and new values, and who changed it.
        AddAudit(report.ReportId, user.AccountId, "EDITED", before, Snapshot(report));

        try
        {
            await _db.SaveChangesAsync(); // saves the edit and its audit entry together
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<ReportDetailDto>.Conflict("This report was changed by someone else. Reload and try again.");
        }

        return await GetByIdAsync(id, user);
    }

    // ---------- Withdraw (DELETE /api/reports/5) ----------

    // A soft delete. Only the reporter, and only while the report is Pending Review.
    public async Task<ServiceResult<bool>> WithdrawAsync(int id, CurrentUser user)
    {
        var report = await WithDetails(VisibleTo(user)).FirstOrDefaultAsync(r => r.ReportId == id);
        if (report == null)
            return ServiceResult<bool>.NotFound($"Report {id} was not found.");

        if (!CanEdit(report, user))
            return ServiceResult<bool>.BadRequest(
                "You can only withdraw your own report while it is Pending Review.", "NOT_EDITABLE");

        var before = Snapshot(report);
        report.IsActive = false;
        report.UpdatedAt = DateTime.UtcNow;
        AddAudit(report.ReportId, user.AccountId, "WITHDRAWN", before, Snapshot(report));

        await _db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    // ---------- Review (POST /api/supervisor/reports/5/review) ----------

    // Supervisors only.
    public async Task<ServiceResult<ReportDetailDto>> ReviewAsync(int id, CreateReviewRequest request, CurrentUser user)
    {
        var decision = ReviewDecisions.Normalise(request.Decision);
        if (decision == null)
            return ServiceResult<ReportDetailDto>.BadRequest("decision must be APPROVE or DENY.", "INVALID_DECISION");

        var report = await WithDetails(VisibleTo(user)).FirstOrDefaultAsync(r => r.ReportId == id);
        if (report == null)
            return ServiceResult<ReportDetailDto>.NotFound($"Report {id} was not found.");

        if (report.ReporterAccountId == user.AccountId)
            return ServiceResult<ReportDetailDto>.BadRequest("You can't review your own report.", "OWN_REPORT");

        if (report.Status.StatusName != ReportStatusNames.PendingReview)
            return ServiceResult<ReportDetailDto>.Conflict(
                $"This report has already been reviewed ({report.Status.StatusName}).", "ALREADY_REVIEWED");

        var reason = (request.Reason ?? request.Comment)?.Trim();
        var denied = decision == ReviewDecisions.Denied;

        // A report can't be approved while expected photos or voice notes are still uploading.
        // Denying is still allowed, for example when the evidence is never going to arrive.
        if (!denied && !EvidenceComplete(report))
            return ServiceResult<ReportDetailDto>.Conflict(
                $"This report is still waiting for evidence ({MissingEvidenceText(report)}). Try again once it has uploaded.",
                "EVIDENCE_INCOMPLETE");
        if (denied && string.IsNullOrWhiteSpace(reason))
            return ServiceResult<ReportDetailDto>.BadRequest("A reason is required when denying a report.", "REASON_REQUIRED");

        var before = Snapshot(report);
        var now = DateTime.UtcNow;

        // 1. The review itself.
        _db.ReportReviews.Add(new ReportReview
        {
            ReportId = report.ReportId,
            ReviewerAccountId = user.AccountId,
            Decision = decision,
            ReviewComment = string.IsNullOrWhiteSpace(reason) ? null : reason,
            ReviewedAt = now
        });

        // 2. The report's new status.
        var newStatus = denied ? ReportStatusNames.Denied : ReportStatusNames.Approved;
        report.StatusId = await GetStatusIdAsync(newStatus);
        report.UpdatedAt = now;

        // 3. The audit entry.
        var after = Snapshot(report);
        after["status"] = newStatus;
        after["reason"] = reason;
        AddAudit(report.ReportId, user.AccountId, decision, before, after);

        // 4. Let the reporter know the outcome.
        AddNotification(report.ReporterAccountId, report.ReportId, NotificationTypes.ReportReviewed,
            $"Your report {report.ReportNumber} was {newStatus.ToLower()}." + (denied ? $" Reason: {reason}" : ""));

        // 5. Approved incidents are also sent to the Safety Officers.
        if (!denied && report.IncidentType.TriggersImmediateAlert)
        {
            foreach (var officerId in (await SafetyOfficerIdsAsync()).Distinct().Where(o => o != user.AccountId))
                AddNotification(officerId, report.ReportId, NotificationTypes.IncidentApproved,
                    $"Incident {report.ReportNumber} at {report.Location.LocationName} was approved by a supervisor.");
        }

        // Everything is saved together or not at all. RowVersion stops two reviews of the same report clashing.
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<ReportDetailDto>.Conflict("Someone else reviewed this report at the same time. Reload it.", "ALREADY_REVIEWED");
        }

        return await GetByIdAsync(id, user);
    }

    // ---------- Audit history (GET /api/reports/5/audit) ----------

    // Safety Officers only.
    public async Task<ServiceResult<List<AuditEntryDto>>> GetAuditAsync(int id, CurrentUser user)
    {
        if (!await VisibleTo(user).AnyAsync(r => r.ReportId == id))
            return ServiceResult<List<AuditEntryDto>>.NotFound($"Report {id} was not found.");

        var entries = await _db.ReportAuditLog.AsNoTracking()
            .Include(a => a.Actor)
            .Where(a => a.ReportId == id)
            .OrderBy(a => a.ActionAt).ThenBy(a => a.AuditLogId)
            .ToListAsync();

        return ServiceResult<List<AuditEntryDto>>.Ok(entries.Select(a => new AuditEntryDto
        {
            AuditLogId = a.AuditLogId,
            Action = a.Action,
            ActorName = a.Actor.FullName,
            ActionAt = a.ActionAt,
            OldValueJson = a.OldValueJson,
            NewValueJson = a.NewValueJson
        }).ToList());
    }

    // ---------- Automatic approval of a Supervisor's own report ----------

    // Rules:
    //   - Only for reports submitted by a Supervisor. This is decided on the server from the
    //     account's current roles, never from anything the app sends.
    //   - Only once every expected photo and voice note has arrived.
    //   - Only from Pending Review, so retries and duplicate uploads can't approve it twice.
    //   - No ReportReviews row is written, since nobody reviewed it. The audit log records AUTO_APPROVED.
    public async Task TryAutoApproveAsync(int reportId, CurrentUser reporter)
    {
        if (!IsSupervisor(reporter))
            return;

        var report = await _db.Reports
            .Include(r => r.IncidentType)
            .Include(r => r.Location)
            .Include(r => r.Status)
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.ReportId == reportId && r.IsActive && r.ReporterAccountId == reporter.AccountId);

        if (report == null || report.Status.StatusName != ReportStatusNames.PendingReview)
            return;

        var (images, voiceNotes) = ReceivedCounts(report);
        if (!CanAutoApprove(report, images, voiceNotes))
            return;

        await ApplyAutoApprovalAsync(report, report.IncidentType, report.Location, reporter.AccountId, AutoApprovedAction,
            "Submitted by a supervisor; approved automatically once all expected evidence had uploaded.");
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed this report at the same moment, for example when the last two
            // uploads arrived together and the other one already approved it. Nothing else to do.
            _db.ChangeTracker.Clear();
        }
    }

    public async Task<(string StatusCode, bool EvidenceComplete)> GetEvidenceStateAsync(int reportId)
    {
        var report = await _db.Reports.AsNoTracking()
            .Include(r => r.Status)
            .Include(r => r.Attachments)
            .FirstAsync(r => r.ReportId == reportId);
        return (Codes.From(report.Status.StatusName), EvidenceComplete(report));
    }

    // Sets the status to Approved, adds the audit entry and, for incidents, notifies the Safety Officers.
    // It doesn't save. The caller saves, so the changes are part of the caller's transaction.
    private async Task ApplyAutoApprovalAsync(Report report, IncidentType type, Location location,
                                              int actorId, string auditAction, string reason)
    {
        var before = Snapshot(report);
        report.StatusId = await GetStatusIdAsync(ReportStatusNames.Approved);
        report.UpdatedAt = DateTime.UtcNow;

        var after = Snapshot(report);
        after["status"] = ReportStatusNames.Approved;
        after["autoApproved"] = true;
        after["reason"] = reason;
        AddAudit(report.ReportId, actorId, auditAction, before, after);

        if (type.TriggersImmediateAlert)
        {
            foreach (var officerId in (await SafetyOfficerIdsAsync()).Distinct().Where(o => o != report.ReporterAccountId))
                AddNotification(officerId, report.ReportId, NotificationTypes.IncidentApproved,
                    $"Incident {report.ReportNumber} at {location.LocationName} was approved automatically (reported by a supervisor).");
        }
    }

    private static bool IsSupervisor(CurrentUser user) => user.IsInRole(RoleNames.Supervisor);

    // Auto-approval needs known expected counts (reports from before migration 003 don't have them)
    // and every expected file to have arrived.
    private static bool CanAutoApprove(Report r, int receivedImages, int receivedVoiceNotes) =>
        r.ExpectedImageCount.HasValue && r.ExpectedVoiceNoteCount.HasValue
        && receivedImages >= r.ExpectedImageCount.Value
        && receivedVoiceNotes >= r.ExpectedVoiceNoteCount.Value;

    private static (int Images, int VoiceNotes) ReceivedCounts(Report r)
    {
        var files = r.Attachments.Where(a => !a.IsDeleted).ToList();
        return (files.Count(a => a.AttachmentType == "PHOTO"), files.Count(a => a.AttachmentType == "VOICE_NOTE"));
    }

    // Unknown expected counts are treated as nothing outstanding.
    private static bool EvidenceComplete(Report r)
    {
        var (images, voiceNotes) = ReceivedCounts(r);
        return images >= (r.ExpectedImageCount ?? 0) && voiceNotes >= (r.ExpectedVoiceNoteCount ?? 0);
    }

    private static string MissingEvidenceText(Report r)
    {
        var (images, voiceNotes) = ReceivedCounts(r);
        var parts = new List<string>();
        var missingImages = (r.ExpectedImageCount ?? 0) - images;
        var missingVoice = (r.ExpectedVoiceNoteCount ?? 0) - voiceNotes;
        if (missingImages > 0) parts.Add($"{missingImages} photo(s)");
        if (missingVoice > 0) parts.Add($"{missingVoice} voice note(s)");
        return string.Join(" and ", parts);
    }

    // ---------- One-off clean-up: Supervisor reports stuck in Pending Review ----------

    // Finds active Pending Review reports whose reporter currently has the Supervisor role.
    //   - Expected counts known and all evidence received: approved (AUTO_APPROVED).
    //   - Expected counts known but evidence missing: left alone, it approves itself when the evidence arrives.
    //   - Created before migration 003 (counts unknown): only approved if an administrator has checked it
    //     and listed its ID in ConfirmLegacyReportIds (AUTO_APPROVED_BY_ADMIN_CHECK, with the admin as the actor).
    // DryRun (the default) changes nothing and only reports what would happen.
    public async Task<List<StuckReportDto>> ResolveStuckSupervisorReportsAsync(StuckReportsRequest request, int adminAccountId)
    {
        var supervisorIds = await _db.AccountRoles
            .Where(ar => ar.IsActive && ar.Account.IsActive && ar.Role.RoleName == RoleNames.Supervisor)
            .Select(ar => ar.AccountId)
            .ToListAsync();

        var stuck = await _db.Reports
            .Include(r => r.Reporter)
            .Include(r => r.IncidentType)
            .Include(r => r.Location)
            .Include(r => r.Status)
            .Include(r => r.Attachments)
            .Where(r => r.IsActive && r.Status.StatusName == ReportStatusNames.PendingReview
                        && supervisorIds.Contains(r.ReporterAccountId))
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();

        var results = new List<StuckReportDto>();
        foreach (var report in stuck)
        {
            var (images, voiceNotes) = ReceivedCounts(report);
            var line = new StuckReportDto
            {
                ReportId = report.ReportId,
                ReportNumber = report.ReportNumber,
                ReporterWorkerId = report.Reporter.WorkerId,
                LocationName = report.Location.LocationName,
                TypeCode = Codes.From(report.IncidentType.TypeName),
                SubmittedAt = report.SubmittedAt,
                ExpectedImageCount = report.ExpectedImageCount,
                ExpectedVoiceNoteCount = report.ExpectedVoiceNoteCount,
                ImageCount = images,
                VoiceNoteCount = voiceNotes
            };

            var legacy = !report.ExpectedImageCount.HasValue || !report.ExpectedVoiceNoteCount.HasValue;
            if (!legacy && !CanAutoApprove(report, images, voiceNotes))
            {
                line.Action = "WAITING_FOR_EVIDENCE";
                line.Explanation = $"Still waiting for {MissingEvidenceText(report)}. It approves itself when the upload arrives.";
            }
            else if (legacy && !request.ConfirmLegacyReportIds.Contains(report.ReportId))
            {
                line.Action = "NEEDS_CONFIRMATION";
                line.Explanation = $"Created before evidence tracking, so the server can't tell if uploads are missing. " +
                                   $"Received {images} photo(s), {voiceNotes} voice note(s). Check with the reporter, then list this ID in confirmLegacyReportIds.";
            }
            else if (request.DryRun)
            {
                line.Action = "WOULD_APPROVE";
                line.Explanation = legacy ? "Confirmed by an administrator." : "All expected evidence has arrived.";
            }
            else
            {
                if (legacy)
                    await ApplyAutoApprovalAsync(report, report.IncidentType, report.Location, adminAccountId, LegacyAutoApprovedAction,
                        "Supervisor report created before evidence tracking; approved after an administrator confirmed its evidence.");
                else
                    await ApplyAutoApprovalAsync(report, report.IncidentType, report.Location, report.ReporterAccountId, AutoApprovedAction,
                        "Submitted by a supervisor; approved automatically (one-time clean-up) once all expected evidence had uploaded.");
                line.Action = "APPROVED";
                line.Explanation = "Approved.";
            }
            results.Add(line);
        }

        if (!request.DryRun)
            await _db.SaveChangesAsync(); // all approvals, audit entries and notifications together

        return results;
    }

    // ---------- Helpers ----------

    // Only the reporter, and only while the report is still waiting for review.
    private static bool CanEdit(Report report, CurrentUser user) =>
        report.ReporterAccountId == user.AccountId && report.Status.StatusName == ReportStatusNames.PendingReview;

    private static bool CanReview(Report report, CurrentUser user) =>
        user.IsInRole(RoleNames.Supervisor)
        && report.ReporterAccountId != user.AccountId
        && report.Status.StatusName == ReportStatusNames.PendingReview
        && user.CanSeeLocation(report.LocationId);

    // Finds the incident type from a code (INCIDENT) or an ID. There are only three rows, so they are all loaded.
    private async Task<IncidentType?> ResolveTypeAsync(string? typeCode, int? incidentTypeId)
    {
        var types = await _db.IncidentTypes.Where(t => t.IsActive).ToListAsync();
        if (!string.IsNullOrWhiteSpace(typeCode))
            return types.FirstOrDefault(t => Codes.From(t.TypeName) == Codes.From(typeCode));
        return types.FirstOrDefault(t => t.IncidentTypeId == incidentTypeId);
    }

    // "PENDING_REVIEW" or "Pending Review" -> "Pending Review"
    private async Task<string> ResolveStatusNameAsync(string statusOrCode)
    {
        if (string.IsNullOrWhiteSpace(statusOrCode))
            return "";
        var names = await _db.ReportStatuses.Select(s => s.StatusName).ToListAsync();
        return names.FirstOrDefault(n => Codes.From(n) == Codes.From(statusOrCode)) ?? statusOrCode;
    }

    private async Task<int> GetStatusIdAsync(string statusName)
    {
        var id = await _db.ReportStatuses.Where(s => s.StatusName == statusName)
                                         .Select(s => (int?)s.StatusId).FirstOrDefaultAsync();
        return id ?? throw new InvalidOperationException(
            $"Report status '{statusName}' is missing from the ReportStatuses table (seed data).");
    }

    private Task<List<int>> SafetyOfficerIdsAsync() => _db.AccountRoles
        .Where(ar => ar.IsActive && ar.Account.IsActive && ar.Role.RoleName == RoleNames.SafetyOfficer)
        .Select(ar => ar.AccountId)
        .ToListAsync();

    private void AddNotification(int recipientId, int reportId, string type, string message)
    {
        _db.Notifications.Add(new Notification
        {
            RecipientAccountId = recipientId,
            ReportId = reportId,
            NotificationType = type,
            Message = message.Length > 500 ? message[..500] : message,
            CreatedAt = DateTime.UtcNow
        });
    }

    // All times are stored in UTC so the phones and the server agree.
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc) // no time zone given, so assume UTC
    };

    // The fields recorded in the audit log. Nothing sensitive is included.
    private static Dictionary<string, object?> Snapshot(Report r) => new()
    {
        ["reportNumber"] = r.ReportNumber,
        ["incidentTypeId"] = r.IncidentTypeId,
        ["locationId"] = r.LocationId,
        ["statusId"] = r.StatusId,
        ["description"] = r.Description,
        ["occurredAt"] = r.OccurredAt,
        ["expectedImageCount"] = r.ExpectedImageCount,
        ["expectedVoiceNoteCount"] = r.ExpectedVoiceNoteCount,
        ["isActive"] = r.IsActive
    };

    public void AddAudit(int reportId, int actorId, string action, object? before, object? after)
    {
        _db.ReportAuditLog.Add(new ReportAuditLog
        {
            ReportId = reportId,
            ActorAccountId = actorId,
            Action = action,
            OldValueJson = before == null ? null : JsonSerializer.Serialize(before),
            NewValueJson = after == null ? null : JsonSerializer.Serialize(after),
            ActionAt = DateTime.UtcNow
        });
    }

    // Fills in the fields shared by the summary and detail DTOs.
    private static T Fill<T>(T dto, Report r) where T : ReportSummaryDto
    {
        var lastReview = r.Reviews.OrderByDescending(rv => rv.ReviewedAt).FirstOrDefault();
        var files = r.Attachments.Where(a => !a.IsDeleted).ToList();

        dto.ReportId = r.ReportId;
        dto.ReportNumber = r.ReportNumber;
        dto.ClientReportId = r.ClientReportId;
        dto.TypeCode = Codes.From(r.IncidentType.TypeName);
        dto.IncidentType = r.IncidentType.TypeName;
        dto.IsUrgent = r.IncidentType.TriggersImmediateAlert;
        dto.LocationId = r.LocationId;
        dto.LocationCode = r.Location.LocationCode;
        dto.LocationName = r.Location.LocationName;
        dto.StatusCode = Codes.From(r.Status.StatusName);
        dto.Status = r.Status.StatusName;
        dto.StatusUpdatedAt = lastReview?.ReviewedAt
            ?? (r.Status.StatusName == ReportStatusNames.PendingReview ? r.SubmittedAt : r.UpdatedAt ?? r.SubmittedAt);
        dto.ReviewReason = lastReview?.Decision == ReviewDecisions.Denied ? lastReview.ReviewComment : null;
        dto.ReporterName = r.Reporter.FullName;
        dto.OccurredAt = r.OccurredAt;
        dto.SubmittedAt = r.SubmittedAt;
        dto.ImageCount = files.Count(a => a.AttachmentType == "PHOTO");
        dto.VoiceNoteCount = files.Count(a => a.AttachmentType == "VOICE_NOTE");
        dto.ExpectedImageCount = r.ExpectedImageCount;
        dto.ExpectedVoiceNoteCount = r.ExpectedVoiceNoteCount;
        dto.EvidenceComplete = EvidenceComplete(r);
        // Approved without an APPROVED review means it was approved automatically.
        dto.AutoApproved = r.Status.StatusName == ReportStatusNames.Approved
                           && !r.Reviews.Any(rv => rv.Decision == ReviewDecisions.Approved);
        return dto;
    }

    private static ReportSummaryDto ToSummary(Report r) => Fill(new ReportSummaryDto(), r);

    private ReportDetailDto ToDetail(Report r, CurrentUser user)
    {
        var dto = Fill(new ReportDetailDto(), r);
        dto.IncidentTypeId = r.IncidentTypeId;
        dto.ReporterAccountId = r.ReporterAccountId;
        dto.ReporterWorkerId = r.Reporter.WorkerId;
        dto.Description = r.Description;
        dto.CreatedAt = r.CreatedAt;
        dto.UpdatedAt = r.UpdatedAt;
        dto.Reviews = r.Reviews.OrderBy(rv => rv.ReviewedAt).Select(rv => new ReviewDto
        {
            ReviewId = rv.ReviewId,
            Decision = rv.Decision,
            Comment = rv.ReviewComment,
            ReviewerName = rv.Reviewer.FullName,
            ReviewedAt = rv.ReviewedAt
        }).ToList();
        dto.Attachments = r.Attachments.Where(a => !a.IsDeleted).OrderBy(a => a.UploadedAt).Select(AttachmentService.ToDto).ToList();
        dto.CanEdit = CanEdit(r, user);
        dto.CanWithdraw = CanEdit(r, user);
        dto.CanReview = CanReview(r, user);

        // Web dashboard link for Safety Officers, for example "http://localhost:5173/incidents/{id}".
        var template = _config["Web:ReportUrlTemplate"];
        if (user.IsInRole(RoleNames.SafetyOfficer) && !string.IsNullOrWhiteSpace(template))
            dto.DashboardUrl = template.Replace("{id}", r.ReportId.ToString());

        return dto;
    }
}
