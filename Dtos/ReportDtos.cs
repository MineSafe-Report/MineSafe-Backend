using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MineSafety.Api.Dtos;

// ---------- WHAT THE APP SENDS ----------

// POST /api/reports : the phone submits a report (straight away, or later when signal returns).
public class CreateReportRequest
{
    // Made by the PHONE when the worker first creates the report, and sent again unchanged on
    // every retry. That's how the server recognises "I've already got this one".
    [Required]
    public Guid ClientReportId { get; set; }

    // Send EITHER typeCode ("INCIDENT", "HAZARD", "NEAR_MISS") OR incidentTypeId.
    public string? TypeCode { get; set; }
    public int? IncidentTypeId { get; set; }

    [Required]
    public int LocationId { get; set; }              // must be one of the worker's locations

    // May be empty when the report has a voice note instead of typed text.
    [MaxLength(4000)]
    public string? Description { get; set; }

    // When it actually happened, in UTC, e.g. "2026-10-19T08:30:00Z".
    [Required]
    public DateTime OccurredAt { get; set; }

    // How many photos / voice notes the phone WILL upload after this report (0 if none).
    // Stored with the report. The evidence is "complete" once they have all arrived:
    //  - a Supervisor's own report is approved automatically at that moment
    //  - nobody can APPROVE a report before that moment
    [Range(0, 50)]
    public int? ExpectedImageCount { get; set; }
    [Range(0, 50)]
    public int? ExpectedVoiceNoteCount { get; set; }
}

// PUT /api/reports/5 : the reporter fixes their own report while it is still Pending Review.
public class UpdateReportRequest
{
    public string? TypeCode { get; set; }
    public int? IncidentTypeId { get; set; }

    [Required]
    public int LocationId { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }

    [Required]
    public DateTime OccurredAt { get; set; }
}

// POST .../review : a supervisor approves or denies.
public class CreateReviewRequest
{
    // "APPROVE" / "DENY" (also accepts "APPROVED" / "DENIED")
    [Required]
    public string Decision { get; set; } = "";

    // The denial reason. Required when denying. ("comment" is accepted too.)
    [MaxLength(1000)]
    public string? Reason { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}

// POST /api/reports/5/attachments  (multipart/form-data)
public class UploadAttachmentRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    // "PHOTO" (or "IMAGE") or "VOICE_NOTE"
    [Required]
    public string AttachmentType { get; set; } = "";

    // The phone's own stable ID for this file (e.g. "image-12", "voice-4"). Sending the same ID
    // again returns the original attachment. Without an ID, an identical file on the same report
    // is recognised by its SHA-256 fingerprint instead. ("localAttachmentId" is accepted too.)
    [MaxLength(100)]
    public string? ClientAttachmentId { get; set; }

    [MaxLength(100)]
    public string? LocalAttachmentId { get; set; }

    public DateTime? CapturedAt { get; set; }
}

// ---------- WHAT THE API SENDS BACK ----------

// One line in a list (My Reports, the review queue, the incident list).
public class ReportSummaryDto
{
    public int ReportId { get; set; }
    public string ReportNumber { get; set; } = "";       // e.g. MS-2026-00042
    public Guid ClientReportId { get; set; }             // lets the phone match it to its local copy
    public string TypeCode { get; set; } = "";           // INCIDENT / HAZARD / NEAR_MISS
    public string IncidentType { get; set; } = "";       // display name: Incident / Hazard / Near Miss
    public bool IsUrgent { get; set; }
    public int LocationId { get; set; }
    public string LocationCode { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string StatusCode { get; set; } = "";         // PENDING_REVIEW / APPROVED / DENIED
    public string Status { get; set; } = "";             // display name
    public DateTime? StatusUpdatedAt { get; set; }       // when the status last changed
    public string? ReviewReason { get; set; }            // the denial reason when DENIED, else null
    public string ReporterName { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int ImageCount { get; set; }                  // photos received so far
    public int VoiceNoteCount { get; set; }              // voice notes received so far
    public int? ExpectedImageCount { get; set; }         // photos the phone said it will send
    public int? ExpectedVoiceNoteCount { get; set; }
    public bool EvidenceComplete { get; set; }           // everything expected has arrived
    public bool AutoApproved { get; set; }               // approved automatically (a Supervisor's own report)
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
    public List<AttachmentDto> Attachments { get; set; } = new();

    // Safety Officers only: link to this report on the web dashboard (no token in it).
    public string? DashboardUrl { get; set; }

    // What THIS user may do with it, so the app knows which buttons to show.
    public bool CanEdit { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanReview { get; set; }
}

// Single reports are always returned wrapped: { "report": { ... } }
public class ReportEnvelope
{
    public ReportDetailDto Report { get; set; } = new();
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
    public string Decision { get; set; } = "";           // APPROVED / DENIED
    public string? Comment { get; set; }
    public string ReviewerName { get; set; } = "";
    public DateTime ReviewedAt { get; set; }
}

// File info only. Never a server path: the file itself comes from GET /api/attachments/{id}.
public class AttachmentDto
{
    public int AttachmentId { get; set; }
    public string AttachmentType { get; set; } = "";     // PHOTO / VOICE_NOTE
    public string OriginalFileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UploadAttachmentResponse
{
    public bool AlreadyExisted { get; set; }             // true = same file was already uploaded
    public AttachmentDto Attachment { get; set; } = new();

    // The report's state after this upload (e.g. a Supervisor's report becomes APPROVED
    // when its last expected file arrives).
    public string ReportStatusCode { get; set; } = "";
    public bool EvidenceComplete { get; set; }
}

// One-time maintenance: Supervisor reports stuck in Pending Review.
public class StuckReportDto
{
    public int ReportId { get; set; }
    public string ReportNumber { get; set; } = "";
    public string ReporterWorkerId { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public DateTime? SubmittedAt { get; set; }
    public int? ExpectedImageCount { get; set; }
    public int? ExpectedVoiceNoteCount { get; set; }
    public int ImageCount { get; set; }
    public int VoiceNoteCount { get; set; }

    // APPROVED | WOULD_APPROVE | WAITING_FOR_EVIDENCE | NEEDS_CONFIRMATION
    public string Action { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public class StuckReportsRequest
{
    // true (default) = only report what WOULD happen; nothing is changed.
    public bool DryRun { get; set; } = true;

    // Reports created before migration 003 have no expected counts, so the server can't know
    // whether evidence is still missing. Approve those ONLY if listed here, after a person has
    // checked them (e.g. the supervisor's phone shows nothing left to upload).
    public List<int> ConfirmLegacyReportIds { get; set; } = new();
}

public class AuditEntryDto
{
    public int AuditLogId { get; set; }
    public string Action { get; set; } = "";             // CREATED, EDITED, WITHDRAWN, APPROVED, DENIED, ATTACHMENT_ADDED
    public string ActorName { get; set; } = "";
    public DateTime ActionAt { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
}

// Dropdown data
public class IncidentTypeDto
{
    public int IncidentTypeId { get; set; }
    public string TypeCode { get; set; } = "";
    public string TypeName { get; set; } = "";
    public bool TriggersImmediateAlert { get; set; }
}

public class ReportStatusDto
{
    public int StatusId { get; set; }
    public string StatusCode { get; set; } = "";
    public string StatusName { get; set; } = "";
    public bool IsTerminal { get; set; }
}
