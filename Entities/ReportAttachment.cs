using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches database/schema/ReportAttachments.sql
// The FILE lives in a folder on the server; this row only stores where it is,
// plus a hash to check it arrived intact.
public class ReportAttachment
{
    public int AttachmentId { get; set; }            // AttachmentID (PK)

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    [MaxLength(50)]
    public string AttachmentType { get; set; } = ""; // PHOTO or VOICE_NOTE

    [MaxLength(255)]
    public string OriginalFileName { get; set; } = "";

    [MaxLength(255)]
    public string StorageKey { get; set; } = "";     // file name on the server, never a client path

    [MaxLength(100)]
    public string MimeType { get; set; } = "";       // e.g. image/jpeg

    public long SizeBytes { get; set; }

    [MaxLength(64)]
    public string? Sha256Hash { get; set; }

    // The phone's own stable ID for this upload (unique per report). A retried upload with the
    // same ID returns the original attachment instead of creating a duplicate.
    [MaxLength(100)]
    public string? ClientAttachmentId { get; set; }

    // SQL column is "UploadedByAccount" (no ID). Mapped explicitly in the DbContext.
    public int UploadedByAccountId { get; set; }
    public Account UploadedBy { get; set; } = null!;

    public DateTime UploadedAt { get; set; }
    public bool IsDeleted { get; set; }
}
