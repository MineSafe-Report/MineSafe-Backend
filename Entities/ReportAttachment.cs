using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches the "ReportAttachments" table. The FILE itself lives in a folder on the server
// (our "object store"); this row only stores where it is plus a hash to check it arrived intact.
public class ReportAttachment
{
    public int AttachmentId { get; set; }            // ERD spells it "AttachementID" (typo)

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    [MaxLength(20)]
    public string AttachmentType { get; set; } = ""; // PHOTO or VOICE_NOTE

    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    [MaxLength(500)]
    public string StorageKey { get; set; } = "";     // path/name of the file on the server

    [MaxLength(100)]
    public string? MimeType { get; set; }            // e.g. image/jpeg

    public long SizeBytes { get; set; }

    [MaxLength(64)]
    public string? Sha256Hash { get; set; }

    public int UploadedByAccountId { get; set; }
    public Account UploadedBy { get; set; } = null!;

    public DateTime UploadedAt { get; set; }
    public bool IsDeleted { get; set; }
}
