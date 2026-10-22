using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the ReportReviews table (database/schema/010_ReportReviews.sql):
// a supervisor's decision to approve or deny a report.
public class ReportReview
{
    public int ReviewId { get; set; }              // ReviewID (primary key)

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int ReviewerAccountId { get; set; }
    public Account Reviewer { get; set; } = null!;

    [MaxLength(50)]
    public string Decision { get; set; } = "";     // APPROVED or DENIED

    [MaxLength(1000)]
    public string? ReviewComment { get; set; }     // the API requires this when a report is denied

    public DateTime ReviewedAt { get; set; }
}
