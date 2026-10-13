using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches the "Reports" table, the main table in the system.
// Not used by any endpoint yet: the Reports module is the next step.
public class Report
{
    public int ReportId { get; set; }

    // ADDED (not in the D3 ERD): the ID the PHONE gives the report when it's created offline.
    // If the phone uploads the same report twice (lost reply, retry), we spot this ID
    // and return the existing report instead of saving a duplicate. Tell Michael.
    public Guid ClientReportId { get; set; }

    [MaxLength(30)]
    public string ReportNumber { get; set; } = "";   // human-friendly reference, e.g. MS-2026-0042

    public int ReporterAccountId { get; set; }
    public Account Reporter { get; set; } = null!;

    public int IncidentTypeId { get; set; }
    public IncidentType IncidentType { get; set; } = null!;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int StatusId { get; set; }
    public ReportStatus Status { get; set; } = null!;

    public string? Description { get; set; }

    // ADDED (not in the D3 ERD): when the event actually happened underground,
    // which can be hours before the phone gets signal and uploads it.
    public DateTime OccurredAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }          // when the phone created it
    public DateTime? UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }       // when the server received it

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public List<ReportAttachment> Attachments { get; set; } = new();
    public List<ReportReview> Reviews { get; set; } = new();
}
