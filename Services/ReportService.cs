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
}

// Filters for GET /api/reports
public class ReportListFilter
{
    // [BindNever] = the app can't set these from the URL; the controller sets them.
    [BindNever] public bool MineOnly { get; set; }       // only reports I submitted (My Reports)
    [BindNever] public bool ReviewQueue { get; set; }    // only Pending Review, urgent first (supervisor list)
    public string? Status { get; set; }
    public int? IncidentTypeId { get; set; }
    public int? LocationId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// All the rules for reports. The biggest service in the API, but every method follows the
// same pattern as LocationService: load, check rules, change, save, return a DTO.
public class ReportService : IReportService
{
    private readonly MineSafetyDbContext _db;

    public ReportService(MineSafetyDbContext db)
    {
        _db = db;
    }

    // =====================================================================
    //  WHO MAY SEE WHICH REPORTS  (used by every read, edit and review)
    // =====================================================================
    //  Safety Officer / Administrator : every report
    //  Supervisor                     : reports at their assigned locations + their own
    //  Worker                         : only their own
    //  Withdrawn reports (IsActive = false) are hidden from everyone.
    private IQueryable<Report> VisibleTo(CurrentUser user)
    {
        var query = _db.Reports.Where(r => r.IsActive);

        if (user.SeesAllLocations)
            return query;

        if (user.IsInRole(RoleNames.Supervisor))
        {
            var locations = user.AllowedLocationIds!;
            return query.Where(r => r.ReporterAccountId == user.AccountId || locations.Contains(r.LocationId));
        }

        return query.Where(r => r.ReporterAccountId == user.AccountId);
    }

    // Reports with everything the DTOs need already loaded.
    private IQueryable<Report> WithDetails(IQueryable<Report> query) => query
        .Include(r => r.Reporter)
        .Include(r => r.IncidentType)
        .Include(r => r.Location)
        .Include(r => r.Status)
        .Include(r => r.Reviews).ThenInclude(rv => rv.Reviewer);

    // =====================================================================
    //  CREATE  (POST /api/reports)
    // =====================================================================
    public async Task<ServiceResult<SubmitReportResponse>> SubmitAsync(CreateReportRequest request, CurrentUser user)
    {
        if (request.ClientReportId == Guid.Empty)
            return ServiceResult<SubmitReportResponse>.BadRequest("ClientReportId is required.");

        var occurredAt = ToUtc(request.OccurredAt);
        if (occurredAt > DateTime.UtcNow.AddMinutes(5))
            return ServiceResult<SubmitReportResponse>.BadRequest("OccurredAt can't be in the future.");

        // 1. DUPLICATE CHECK. Have we already got a report with this ClientReportId?
        //    This happens when the phone uploaded it, but lost signal before our reply arrived,
        //    so it tries again. We must NOT save it twice.
        var duplicate = await CheckForDuplicateAsync(request, occurredAt, user);
        if (duplicate != null)
            return duplicate;

        // 2. Normal checks.
        var incidentType = await _db.IncidentTypes.FirstOrDefaultAsync(t => t.IncidentTypeId == request.IncidentTypeId && t.IsActive);
        if (incidentType == null)
            return ServiceResult<SubmitReportResponse>.BadRequest("Unknown incident type.");

        var location = await _db.Locations.FirstOrDefaultAsync(l => l.LocationId == request.LocationId && l.IsActive);
        if (location == null || !user.CanSeeLocation(location.LocationId))
            return ServiceResult<SubmitReportResponse>.BadRequest("You can't report at this location.");

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

        // 3. Save the report, its number, the audit row and the notifications in ONE transaction:
        //    either all of it is saved, or none of it.
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
            // Forget our failed attempt and answer exactly like a normal retry.
            _db.ChangeTracker.Clear();
            var retry = await CheckForDuplicateAsync(request, occurredAt, user);
            if (retry != null)
                return retry;
            throw;
        }

        var saved = await GetByIdAsync(report.ReportId, user);
        return ServiceResult<SubmitReportResponse>.Ok(new SubmitReportResponse { AlreadyExisted = false, Report = saved.Value! });
    }

    // null = not a duplicate. Otherwise: the answer to send back.
    private async Task<ServiceResult<SubmitReportResponse>?> CheckForDuplicateAsync(
        CreateReportRequest request, DateTime occurredAt, CurrentUser user)
    {
        var existing = await _db.Reports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ClientReportId == request.ClientReportId);
        if (existing == null)
            return null;

        // Same ID but different person or different content = not a retry, something is wrong.
        var sameReport = existing.ReporterAccountId == user.AccountId
            && existing.IncidentTypeId == request.IncidentTypeId
            && existing.LocationId == request.LocationId
            && existing.Description == request.Description.Trim()
            && Math.Abs((existing.OccurredAt - occurredAt).TotalSeconds) < 1;

        if (!sameReport)
            return ServiceResult<SubmitReportResponse>.Conflict(
                "This ClientReportId was already used for a different report. Generate a new ID for a new report.");

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
    //  - for urgent types (Incident): every Safety Officer as well
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
        {
            var safetyOfficers = await _db.AccountRoles
                .Where(ar => ar.IsActive && ar.Account.IsActive && ar.Role.RoleName == RoleNames.SafetyOfficer)
                .Select(ar => ar.AccountId)
                .ToListAsync();
            recipients.AddRange(safetyOfficers);
        }

        var message = urgent
            ? $"URGENT: {type.TypeName} reported at {location.LocationName} ({report.ReportNumber})."
            : $"New {type.TypeName} report at {location.LocationName} ({report.ReportNumber}).";

        foreach (var accountId in recipients.Distinct().Where(id => id != reporterId))
        {
            _db.Notifications.Add(new Notification
            {
                RecipientAccountId = accountId,
                ReportId = report.ReportId,
                NotificationType = urgent ? NotificationTypes.IncidentAlert : NotificationTypes.NewReport,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    // =====================================================================
    //  READ  (GET /api/reports, GET /api/reports/my, GET /api/reports/5)
    // =====================================================================
    public async Task<PagedResult<ReportSummaryDto>> ListAsync(CurrentUser user, ReportListFilter filter)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = VisibleTo(user);

        if (filter.MineOnly)
            query = query.Where(r => r.ReporterAccountId == user.AccountId);
        if (filter.ReviewQueue)
            query = query.Where(r => r.Status.StatusName == ReportStatusNames.PendingReview
                                     && r.ReporterAccountId != user.AccountId);   // you can't review your own
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(r => r.Status.StatusName == filter.Status);
        if (filter.IncidentTypeId.HasValue)
            query = query.Where(r => r.IncidentTypeId == filter.IncidentTypeId.Value);
        if (filter.LocationId.HasValue)
            query = query.Where(r => r.LocationId == filter.LocationId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(r => r.ReportNumber.Contains(s) || r.Description.Contains(s));
        }

        var total = await query.CountAsync();

        // Review queue: urgent first, then oldest first (first come, first served).
        // Everything else: newest first.
        query = filter.ReviewQueue
            ? query.OrderByDescending(r => r.IncidentType.TriggersImmediateAlert).ThenBy(r => r.SubmittedAt)
            : query.OrderByDescending(r => r.SubmittedAt);

        var reports = await WithDetails(query.AsNoTracking())
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ReportSummaryDto>
        {
            Items = reports.Select(ToSummary).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<ServiceResult<ReportDetailDto>> GetByIdAsync(int id, CurrentUser user)
    {
        // VisibleTo means: if you're not allowed to see it, it simply isn't found (404).
        var report = await WithDetails(VisibleTo(user).AsNoTracking()).FirstOrDefaultAsync(r => r.ReportId == id);

        return report == null
            ? ServiceResult<ReportDetailDto>.NotFound($"Report {id} was not found.")
            : ServiceResult<ReportDetailDto>.Ok(ToDetail(report, user));
    }

    // =====================================================================
    //  UPDATE  (PUT /api/reports/5)
    // =====================================================================
    public async Task<ServiceResult<ReportDetailDto>> UpdateAsync(int id, UpdateReportRequest request, CurrentUser user)
    {
        var report = await WithDetails(VisibleTo(user)).FirstOrDefaultAsync(r => r.ReportId == id);
        if (report == null)
            return ServiceResult<ReportDetailDto>.NotFound($"Report {id} was not found.");

        if (!CanEdit(report, user))
            return ServiceResult<ReportDetailDto>.BadRequest(
                "You can only edit your own report while it is Pending Review (Safety Officers can edit any report).");

        var occurredAt = ToUtc(request.OccurredAt);
        if (occurredAt > DateTime.UtcNow.AddMinutes(5))
            return ServiceResult<ReportDetailDto>.BadRequest("OccurredAt can't be in the future.");

        if (!await _db.IncidentTypes.AnyAsync(t => t.IncidentTypeId == request.IncidentTypeId && t.IsActive))
            return ServiceResult<ReportDetailDto>.BadRequest("Unknown incident type.");

        if (!await _db.Locations.AnyAsync(l => l.LocationId == request.LocationId && l.IsActive)
            || !user.CanSeeLocation(request.LocationId))
            return ServiceResult<ReportDetailDto>.BadRequest("You can't use this location.");

        var before = Snapshot(report);

        report.IncidentTypeId = request.IncidentTypeId;
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
    //  DELETE (soft)  (DELETE /api/reports/5)
    // =====================================================================
    // "Withdraw" a report: IsActive = false. The row and its history stay in the database,
    // it just disappears from every list.
    public async Task<ServiceResult<bool>> WithdrawAsync(int id, CurrentUser user)
    {
        var report = await WithDetails(VisibleTo(user)).FirstOrDefaultAsync(r => r.ReportId == id);
        if (report == null)
            return ServiceResult<bool>.NotFound($"Report {id} was not found.");

        if (!CanEdit(report, user))
            return ServiceResult<bool>.BadRequest(
                "You can only withdraw your own report while it is Pending Review (Safety Officers can withdraw any report).");

        var before = Snapshot(report);
        report.IsActive = false;
        report.UpdatedAt = DateTime.UtcNow;
        AddAudit(report.ReportId, user.AccountId, "WITHDRAWN", before, Snapshot(report));

        await _db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    // =====================================================================
    //  REVIEW  (POST /api/reports/5/reviews)
    // =====================================================================
    public async Task<ServiceResult<ReportDetailDto>> ReviewAsync(int id, CreateReviewRequest request, CurrentUser user)
    {
        var report = await WithDetails(VisibleTo(user)).FirstOrDefaultAsync(r => r.ReportId == id);
        if (report == null)
            return ServiceResult<ReportDetailDto>.NotFound($"Report {id} was not found.");

        if (report.ReporterAccountId == user.AccountId)
            return ServiceResult<ReportDetailDto>.BadRequest("You can't review your own report.");

        if (report.Status.StatusName != ReportStatusNames.PendingReview)
            return ServiceResult<ReportDetailDto>.Conflict($"This report has already been reviewed ({report.Status.StatusName}).");

        var denied = request.Decision == ReviewDecisions.Denied;
        if (denied && string.IsNullOrWhiteSpace(request.Comment))
            return ServiceResult<ReportDetailDto>.BadRequest("A reason is required when denying a report.");

        var before = Snapshot(report);
        var now = DateTime.UtcNow;

        // 1. the review itself
        _db.ReportReviews.Add(new ReportReview
        {
            ReportId = report.ReportId,
            ReviewerAccountId = user.AccountId,
            Decision = request.Decision,
            ReviewComment = request.Comment?.Trim(),
            ReviewedAt = now
        });

        // 2. the report's new status
        var newStatus = denied ? ReportStatusNames.Denied : ReportStatusNames.Approved;
        report.StatusId = await GetStatusIdAsync(newStatus);
        report.UpdatedAt = now;

        // 3. the audit row
        var after = Snapshot(report);
        after["status"] = newStatus;
        after["reviewComment"] = request.Comment?.Trim();
        AddAudit(report.ReportId, user.AccountId, request.Decision, before, after);

        // 4. tell the worker (their "Report confirmation" user story)
        _db.Notifications.Add(new Notification
        {
            RecipientAccountId = report.ReporterAccountId,
            ReportId = report.ReportId,
            NotificationType = NotificationTypes.ReportReviewed,
            Message = $"Your report {report.ReportNumber} was {newStatus.ToLower()}."
                      + (denied ? $" Reason: {request.Comment!.Trim()}" : ""),
            CreatedAt = now
        });

        // All four saved together, or not at all.
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<ReportDetailDto>.Conflict("Someone else reviewed this report at the same time. Reload it.");
        }

        return await GetByIdAsync(id, user);
    }

    // =====================================================================
    //  AUDIT HISTORY  (GET /api/reports/5/audit)
    // =====================================================================
    public async Task<ServiceResult<List<AuditEntryDto>>> GetAuditAsync(int id, CurrentUser user)
    {
        if (!await VisibleTo(user).AnyAsync(r => r.ReportId == id)
            && !await _db.Reports.AnyAsync(r => r.ReportId == id && !r.IsActive && user.SeesAllLocations))
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

    // Reporter: own report, only while Pending Review. Safety Officer: any report, any time.
    private static bool CanEdit(Report report, CurrentUser user) =>
        user.IsInRole(RoleNames.SafetyOfficer)
        || (report.ReporterAccountId == user.AccountId && report.Status.StatusName == ReportStatusNames.PendingReview);

    private static bool CanReview(Report report, CurrentUser user) =>
        (user.IsInRole(RoleNames.Supervisor) || user.IsInRole(RoleNames.SafetyOfficer))
        && report.ReporterAccountId != user.AccountId
        && report.Status.StatusName == ReportStatusNames.PendingReview;

    private async Task<int> GetStatusIdAsync(string statusName)
    {
        var id = await _db.ReportStatuses.Where(s => s.StatusName == statusName)
                                         .Select(s => (int?)s.StatusId).FirstOrDefaultAsync();
        return id ?? throw new InvalidOperationException(
            $"Report status '{statusName}' is missing from the ReportStatuses table (seed data).");
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

    private void AddAudit(int reportId, int actorId, string action,
                          Dictionary<string, object?>? before, Dictionary<string, object?>? after)
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

    private static ReportSummaryDto ToSummary(Report r) => new()
    {
        ReportId = r.ReportId,
        ReportNumber = r.ReportNumber,
        ClientReportId = r.ClientReportId,
        IncidentType = r.IncidentType.TypeName,
        IsUrgent = r.IncidentType.TriggersImmediateAlert,
        LocationId = r.LocationId,
        LocationName = r.Location.LocationName,
        Status = r.Status.StatusName,
        ReporterName = r.Reporter.FullName,
        OccurredAt = r.OccurredAt,
        SubmittedAt = r.SubmittedAt
    };

    private static ReportDetailDto ToDetail(Report r, CurrentUser user) => new()
    {
        ReportId = r.ReportId,
        ReportNumber = r.ReportNumber,
        ClientReportId = r.ClientReportId,
        IncidentTypeId = r.IncidentTypeId,
        IncidentType = r.IncidentType.TypeName,
        IsUrgent = r.IncidentType.TriggersImmediateAlert,
        LocationId = r.LocationId,
        LocationName = r.Location.LocationName,
        Status = r.Status.StatusName,
        ReporterAccountId = r.ReporterAccountId,
        ReporterName = r.Reporter.FullName,
        ReporterWorkerId = r.Reporter.WorkerId,
        Description = r.Description,
        OccurredAt = r.OccurredAt,
        SubmittedAt = r.SubmittedAt,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        Reviews = r.Reviews.OrderBy(rv => rv.ReviewedAt).Select(rv => new ReviewDto
        {
            ReviewId = rv.ReviewId,
            Decision = rv.Decision,
            Comment = rv.ReviewComment,
            ReviewerName = rv.Reviewer.FullName,
            ReviewedAt = rv.ReviewedAt
        }).ToList(),
        CanEdit = CanEdit(r, user),
        CanWithdraw = CanEdit(r, user),
        CanReview = CanReview(r, user)
    };
}
