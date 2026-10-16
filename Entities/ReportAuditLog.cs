using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches database/schema/011_ReportAuditLog.sql : history of everything done to a report.
public class ReportAuditLog
{
    public int AuditLogId { get; set; }              // AuditLogID (PK)

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int ActorAccountId { get; set; }          // who did it
    public Account Actor { get; set; } = null!;

    [MaxLength(50)]
    public string Action { get; set; } = "";         // CREATED, EDITED, APPROVED, DENIED ...

    public string? OldValueJson { get; set; }        // must be valid JSON (SQL CHECK constraint)
    public string? NewValueJson { get; set; }
    public DateTime ActionAt { get; set; }
}
