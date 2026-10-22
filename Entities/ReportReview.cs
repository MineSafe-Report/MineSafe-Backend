using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches database/schema/ReportReviews.sql : a supervisor's approve/deny decision.
public class ReportReview
{
    public int ReviewId { get; set; }                // ReviewID (PK)

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int ReviewerAccountId { get; set; }
    public Account Reviewer { get; set; } = null!;

    [MaxLength(50)]
    public string Decision { get; set; } = "";       // APPROVED or DENIED

    [MaxLength(1000)]
    public string? ReviewComment { get; set; }       // required by the API when denied

    public DateTime ReviewedAt { get; set; }
}
