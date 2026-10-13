using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches the "ReportStatuses" table: Pending Review, Approved, Denied.
// ("Pending upload" is NOT here on purpose: that only exists on the phone before sync.)
public class ReportStatus
{
    public int StatusId { get; set; }

    [MaxLength(50)]
    public string StatusName { get; set; } = "";

    public bool IsTerminal { get; set; }             // true = final status, nothing happens after it
    public int DisplayOrder { get; set; }
}
