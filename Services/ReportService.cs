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

    // Shared with AttachmentService so both use the SAME "who may see this report" rule.
    IQueryable<Report> VisibleTo(CurrentUser user);
    void AddAudit(int reportId, int actorId, string action, object? before, object? after);
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

    // [BindNever] = the app can't set these from the URL; the controller sets them per route.
    [BindNever] public bool MineOnly { get; set; }             // only reports I submitted
    [BindNever] public bool AssignedLocationsOnly { get; set; } // supervisor view: my locations, not my own reports
    [BindNever] public bool IncidentsOnly { get; set; }         // Safety Officer view
}

// All the rules for reports. Every method follows the same pattern as LocationService:
// load, check rules, change, save, return a DTO.
public class ReportService : IReportService
{
    private readonly MineSafetyDbContext _db;
    private readonly IConfiguration _config;

    public ReportService(MineSafetyDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // =====================================================================
    //  WHO MAY SEE WHICH REPORTS  (used by every read, edit, review and file download)
    // =====================================================================
    //  Everyone        : their own reports
    //  Supervisor      : + every report at their assigned locations
    //  Safety Officer  : + every INCIDENT in the mine that isn't denied (read-only)
    //  Administrator   : no report access (manages accounts and locations only)
    //  Withdrawn reports (IsActive = false) are hidden from everyone.
    public IQueryable<Report> VisibleTo(CurrentUser user)
    {
        var isSupervisor = user.IsInRole(RoleNames.Supervisor);
        var isOfficer = user.IsInRole(RoleNames.SafetyOfficer);
        var supervisorSeesAll = isSupervisor && user.AllowedLocationIds == null;
        var locations = user.AllowedLocationIds ?? new List<int>();

        return _db.Reports.Where(r => r.IsActive && (
            r.ReporterAccountId == user.AccountId
            || (isSupervisor && (supervisorSeesAll || locations.Contains(r.LocationId)))
            || (isOfficer && r.IncidentType.TriggersImmediateAlert && r.Status.StatusName != ReportStatusNames.Denied)));
    }

    private IQueryable<Report> WithDetails(IQueryable<Report> query) => query
        .Include(r => r.Reporter)
        .Include(r => r.IncidentType)
        .Include(r => r.Location)
        .Include(r => r.Status)
        .Include(r => r.Reviews).ThenInclude(rv => rv.Reviewer)
        .Include(r => r.Attachments);

    // =====================================================================
    //  CREATE  (POST /api/reports)
    // =====================================================================
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

        // 1. DUPLICATE CHECK. Have we already got a report with this ClientReportId?
        //    This happens when the phone uploaded it, but lost signal before our reply arrived,
        //    so it tries again. We must NOT save it twice.
        var duplicate = await CheckForDuplicateAsync(request, incidentType.IncidentTypeId, occurredAt, user);
        if (duplicate != null)
            return duplicate;

        // 2. Normal checks.
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.LocationId == request.LocationId && l.IsActive);
        if (location == null || !user.CanSeeLocation(location.LocationId))
            return ServiceResult<SubmitReportResponse>.BadRequest("You can't report at this location.", "LOCATION_NOT_ALLOWED");

        var pendingStatusId = await GetStatusIdAsync(ReportStatusNames.PendingReview);
        var now = DateTime.UtcNow;

        var report = new Report
        {
            ClientReportId = request.ClientReportId,
            // The reporter is ALWAYS the logged-in user, never something the app sends.
            ReporterAccountId = user.AccountId,
            IncidentTypeId = incidentType.IncidentTypeId,
            LocationId = location.LocationId,
            StatusId = pendingStatusId,
            Description = request.Description.Trim(),
            OccurredAt = occurredAt,
            IsActive = true,
            CreatedAt = now,
            SubmittedAt = now,
            // ReportNumber must be unique and can't be empty, but the nice number (MS-2026-00042)
            // uses the ReportID, which SQL only gives us after the INSERT. So we save a temporary
            // number first (unique because ClientReportId is unique), then replace it.
            ReportNumber = "TMP-" + request.ClientReportId.ToString("N")
        };

        // 3. Save the report, its number, the audit row and the notifications in ONE transaction.
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            _db.Reports.Add(report);
            await _db.SaveChangesAsync();                          // INSERT, SQL fills in ReportId

            report.ReportNumber = $"MS-{now:yyyy}-{report.ReportId:D5}";
            AddAudit(report.ReportId, user.AccountId, "CREATED", null, Snapshot(report));
            await AddNewReportNotificationsAsync(report, incidentType, location, user.AccountId);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Two copies of the same report arrived at the same moment and the other one won.
            // The database's UNIQUE rule on ClientReportId stopped this one: our final safety net.
            _db.ChangeTracker.Clear();
            var retry = await CheckForDuplicateAsync(request, incidentType.IncidentTypeId, occurredAt, user);
            if (retry != null)
                return retry;
            throw;
        }

        var saved = await LoadDetailAsync(report.ReportId, user);
        return ServiceResult<SubmitReportResponse>.Ok(new SubmitReportResponse { AlreadyExisted = false, Report = saved! });
    }

    // null = not a duplicate. Otherwise: the answer to send back.
    private async Task<ServiceResult<SubmitReportResponse>?> CheckForDuplicateAsync(
        CreateReportRequest request, int incidentTypeId, DateTime occurredAt, CurrentUser user)
    {
        var existing = await _db.Reports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ClientReportId == request.ClientReportId);
        if (existing == null)
            return null;

        // Same ID but different person or different content = not a retry, something is wrong.
        var sameReport = existing.ReporterAccountId == user.AccountId
            && existing.IncidentTypeId == incidentTypeId
            && existing.LocationId == request.LocationId
            && existing.Description == request.Description.Trim()
            && Math.Abs((existing.OccurredAt - occurredAt).TotalSeconds) < 1;

        if (!sameReport)
            return ServiceResult<SubmitReportResponse>.Conflict(
                "This clientReportId was already used for a different report. Generate a new ID for a new report.",
                "CLIENT_ID_CONFLICT");

        // A genuine retry: hand back the report we already have, unchanged.
        var details = await WithDetails(_db.Reports.AsNoTracking()).FirstAsync(r => r.ReportId == existing.ReportId);
        return ServiceResult<SubmitReportResponse>.Ok(new SubmitReportResponse
        {
            AlreadyExisted = true,
            Report = ToDetail(details, user)
        });
    }

    // Who gets told about a new report:
    //  - every Supervisor assigned to that location
    //  - for Incidents: every Safety Officer as well
    // The reporter themselves is never notified about their own report.
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

    // =====================================================================
    //  READ
    // =====================================================================
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
            query = query.Where(r => r.ReporterAccountId != user.AccountId);      // you can't review your own
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

        // Pending reviews: urgent first, then oldest first (first come, first served).
        // Everything else: newest first.
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
        // VisibleTo means: if you're not allowed to see it, it simply isn't found (404).
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

    // =====================================================================
    //  UPDATE  (PUT /api/reports/5) : the reporter, only while Pending Review
    // =====================================================================
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
        report.Description = request.Description.Trim();
        report.OccurredAt = occurredAt;
        report.UpdatedAt = DateTime.UtcNow;

        // The audit row records exactly what changed, from what, to what, and by whom.
        AddAudit(report.ReportId, user.AccountId, "EDITED", before, Snapshot(report));

        try
        {
            await _db.SaveChangesAsync();        // the edit + its audit row, together
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<ReportDetailDto>.Conflict("This report was changed by someone else. Reload and try again.");
        }

        return await GetByIdAsync(id, user);
    }

    // =====================================================================
    //  DELETE (soft)  (DELETE /api/reports/5) : the reporter, only while Pending Review
    // =====================================================================
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

    // =====================================================================
    //  REVIEW  (POST /api/supervisor/reports/5/review) : Supervisors only
    // =====================================================================
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
        if (denied && string.IsNullOrWhiteSpace(reason))
            return ServiceResult<ReportDetailDto>.BadRequest("A reason is required when denying a report.", "REASON_REQUIRED");

        var before = Snapshot(report);
        var now = DateTime.UtcNow;

        // 1. the review itself
        _db.ReportReviews.Add(new ReportReview
        {
            ReportId = report.ReportId,
            ReviewerAccountId = user.AccountId,
            Decision = decision,
            ReviewComment = string.IsNullOrWhiteSpace(reason) ? null : reason,
            ReviewedAt = now
        });

        // 2. the report's new status
        var newStatus = denied ? ReportStatusNames.Denied : ReportStatusNames.Approved;
        report.StatusId = await GetStatusIdAsync(newStatus);
        report.UpdatedAt = now;

        // 3. the audit row
        var after = Snapshot(report);
        after["status"] = newStatus;
        after["reason"] = reason;
        AddAudit(report.ReportId, user.AccountId, decision, before, after);

        // 4. tell the worker (their "Report confirmation" user story)
        AddNotification(report.ReporterAccountId, report.ReportId, NotificationTypes.ReportReviewed,
            $"Your report {report.ReportNumber} was {newStatus.ToLower()}." + (denied ? $" Reason: {reason}" : ""));

        // 5. an APPROVED Incident is also reported to the Safety Officers
        if (!denied && report.IncidentType.TriggersImmediateAlert)
        {
            foreach (var officerId in (await SafetyOfficerIdsAsync()).Distinct().Where(o => o != user.AccountId))
                AddNotification(officerId, report.ReportId, NotificationTypes.IncidentApproved,
                    $"Incident {report.ReportNumber} at {report.Location.LocationName} was approved by a supervisor.");
        }

        // All of it saved together, or not at all. RowVersion stops two reviews racing each other.
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

    // =====================================================================
    //  AUDIT HISTORY  (GET /api/reports/5/audit) : Safety Officers
    // =====================================================================
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

    // =====================================================================
    //  SMALL HELPERS
    // =====================================================================

    // Only the reporter, only while it's still waiting for review.
    private static bool CanEdit(Report report, CurrentUser user) =>
        report.ReporterAccountId == user.AccountId && report.Status.StatusName == ReportStatusNames.PendingReview;

    private static bool CanReview(Report report, CurrentUser user) =>
        user.IsInRole(RoleNames.Supervisor)
        && report.ReporterAccountId != user.AccountId
        && report.Status.StatusName == ReportStatusNames.PendingReview
        && user.CanSeeLocation(report.LocationId);

    // Finds the incident type from a code (INCIDENT) or an ID. There are only 3 rows, so we just load them.
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

    // Store every time in UTC so all phones and the server agree.
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)      // no zone given: assume UTC
    };

    // The fields we record in the audit log (never anything secret).
    private static Dictionary<string, object?> Snapshot(Report r) => new()
    {
        ["reportNumber"] = r.ReportNumber,
        ["incidentTypeId"] = r.IncidentTypeId,
        ["locationId"] = r.LocationId,
        ["statusId"] = r.StatusId,
        ["description"] = r.Description,
        ["occurredAt"] = r.OccurredAt,
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

    // Fills in everything a list line needs. Used for both summary and detail.
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
        dto.StatusUpdatedAt = lastReview?.ReviewedAt ?? r.SubmittedAt;
        dto.ReviewReason = lastReview?.Decision == ReviewDecisions.Denied ? lastReview.ReviewComment : null;
        dto.ReporterName = r.Reporter.FullName;
        dto.OccurredAt = r.OccurredAt;
        dto.SubmittedAt = r.SubmittedAt;
        dto.ImageCount = files.Count(a => a.AttachmentType == "PHOTO");
        dto.VoiceNoteCount = files.Count(a => a.AttachmentType == "VOICE_NOTE");
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

        // Web dashboard link for Safety Officers, e.g. "http://localhost:5173/incidents/{id}".
        var template = _config["Web:ReportUrlTemplate"];
        if (user.IsInRole(RoleNames.SafetyOfficer) && !string.IsNullOrWhiteSpace(template))
            dto.DashboardUrl = template.Replace("{id}", r.ReportId.ToString());

        return dto;
    }
}
