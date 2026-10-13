using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches the "ReportReviews" table: a supervisor's approve/deny decision.
public class ReportReview
{
    public int ReviewId { get; set; }

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int ReviewerAccountId { get; set; }
    public Account Reviewer { get; set; } = null!;

    [MaxLength(20)]
    public string Decision { get; set; } = "";       // APPROVED or DENIED

    public string? ReviewComment { get; set; }       // required when denied (denial reason)
    public DateTime ReviewedAt { get; set; }
}
