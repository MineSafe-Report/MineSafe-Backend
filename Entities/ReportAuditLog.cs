using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches the "ReportAuditLog" table: a history of everything that happened to a report.
public class ReportAuditLog
{
    public int AuditLogId { get; set; }

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int ActorAccountId { get; set; }          // who did it
    public Account Actor { get; set; } = null!;

    [MaxLength(50)]
    public string Action { get; set; } = "";         // CREATED, EDITED, APPROVED, DENIED ...

    public string? OldValueJson { get; set; }        // what it looked like before (as JSON text)
    public string? NewValueJson { get; set; }        // what it looks like after
    public DateTime ActionAt { get; set; }
}
