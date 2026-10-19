using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Dtos;

// ---------- WHAT THE APP SENDS ----------

// POST /api/reports : the phone submits a report (straight away, or later when signal returns).
public class CreateReportRequest
{
    // Made by the PHONE when the worker first creates the report, and sent again unchanged on
    // every retry. That's how the server recognises "I've already got this one".
    [Required]
    public Guid ClientReportId { get; set; }

    [Required]
    public int IncidentTypeId { get; set; }          // Hazard / Incident / Near Miss (GET /api/incident-types)

    [Required]
    public int LocationId { get; set; }              // must be one of the worker's locations

    [Required, MinLength(3), MaxLength(4000)]
    public string Description { get; set; } = "";

    // When it actually happened. Send it with a time zone, e.g. "2026-10-13T09:30:00Z"
    // or "2026-10-13T11:30:00+02:00". The server stores it in UTC.
    [Required]
    public DateTime OccurredAt { get; set; }
}

// PUT /api/reports/5 : the reporter fixes their report while it is still Pending Review,
// or a Safety Officer corrects it at any time. Send the full new values.
public class UpdateReportRequest
{
    [Required]
    public int IncidentTypeId { get; set; }

    [Required]
    public int LocationId { get; set; }

    [Required, MinLength(3), MaxLength(4000)]
    public string Description { get; set; } = "";

    [Required]
    public DateTime OccurredAt { get; set; }
}

// POST /api/reports/5/reviews : a supervisor approves or denies.
public class CreateReviewRequest
{
    // "APPROVED" or "DENIED"
    [Required, RegularExpression("^(APPROVED|DENIED)$", ErrorMessage = "Decision must be APPROVED or DENIED.")]
    public string Decision { get; set; } = "";

    // Required when the decision is DENIED (the denial reason from D3).
    [MaxLength(1000)]
    public string? Comment { get; set; }
}

// ---------- WHAT THE API SENDS BACK ----------

// One line in a list (My Reports, the review queue).
public class ReportSummaryDto
{
    public int ReportId { get; set; }
    public string ReportNumber { get; set; } = "";       // e.g. MS-2026-00042
    public Guid ClientReportId { get; set; }             // lets the phone match it to its local copy
    public string IncidentType { get; set; } = "";
    public bool IsUrgent { get; set; }                   // true for types that trigger an alert (Incident)
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string Status { get; set; } = "";             // Pending Review / Approved / Denied
    public string ReporterName { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

// Everything about one report (the report detail screen).
public class ReportDetailDto : ReportSummaryDto
{
    public int IncidentTypeId { get; set; }
    public int ReporterAccountId { get; set; }
    public string ReporterWorkerId { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ReviewDto> Reviews { get; set; } = new();

    // What THIS user may do with it, so the app knows which buttons to show.
    public bool CanEdit { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanReview { get; set; }
}

// The answer to POST /api/reports
public class SubmitReportResponse
{
    // false = newly saved (201). true = we already had it from an earlier attempt (200).
    // Either way the phone can mark its local copy as synced.
    public bool AlreadyExisted { get; set; }
    public ReportDetailDto Report { get; set; } = new();
}

public class ReviewDto
{
    public int ReviewId { get; set; }
    public string Decision { get; set; } = "";
    public string? Comment { get; set; }
    public string ReviewerName { get; set; } = "";
    public DateTime ReviewedAt { get; set; }
}

public class AuditEntryDto
{
    public int AuditLogId { get; set; }
    public string Action { get; set; } = "";             // CREATED, EDITED, WITHDRAWN, APPROVED, DENIED
    public string ActorName { get; set; } = "";
    public DateTime ActionAt { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
}

// Dropdown data
public class IncidentTypeDto
{
    public int IncidentTypeId { get; set; }
    public string TypeName { get; set; } = "";
    public bool TriggersImmediateAlert { get; set; }
}

public class ReportStatusDto
{
    public int StatusId { get; set; }
    public string StatusName { get; set; } = "";
    public bool IsTerminal { get; set; }
}
