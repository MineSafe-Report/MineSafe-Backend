using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the ReportAuditLog table (database/schema/ReportAuditLog.sql):
// the history of everything that happened to a report.
public class ReportAuditLog
{
    public int AuditLogId { get; set; }            // AuditLogID (primary key)

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int ActorAccountId { get; set; }        // the account that performed the action
    public Account Actor { get; set; } = null!;

    [MaxLength(50)]
    public string Action { get; set; } = "";       // CREATED, EDITED, APPROVED, DENIED, ...

    public string? OldValueJson { get; set; }      // must be valid JSON (checked by a SQL constraint)
    public string? NewValueJson { get; set; }
    public DateTime ActionAt { get; set; }
}
