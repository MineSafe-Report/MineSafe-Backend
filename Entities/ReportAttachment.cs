using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the ReportAttachments table (database/schema/009_ReportAttachments.sql).
// The file itself is saved in a folder on the server. This row stores where it is,
// plus a hash to confirm the file arrived intact.
public class ReportAttachment
{
    public int AttachmentId { get; set; }          // AttachmentID (primary key)

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    [MaxLength(50)]
    public string AttachmentType { get; set; } = ""; // PHOTO or VOICE_NOTE

    [MaxLength(255)]
    public string OriginalFileName { get; set; } = "";

    [MaxLength(255)]
    public string StorageKey { get; set; } = "";   // the file's name on the server, never a path from the phone

    [MaxLength(100)]
    public string MimeType { get; set; } = "";     // for example image/jpeg

    public long SizeBytes { get; set; }

    [MaxLength(64)]
    public string? Sha256Hash { get; set; }

    // The phone's own ID for this upload (unique per report). Retrying an upload with the
    // same ID returns the original attachment instead of creating a duplicate.
    [MaxLength(100)]
    public string? ClientAttachmentId { get; set; }

    // The SQL column is "UploadedByAccount" (without "ID"). Mapped in MineSafetyDbContext.
    public int UploadedByAccountId { get; set; }
    public Account UploadedBy { get; set; } = null!;

    public DateTime UploadedAt { get; set; }
    public bool IsDeleted { get; set; }
}
