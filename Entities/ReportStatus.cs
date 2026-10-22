using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches database/schema/ReportStatuses.sql : Pending Review, Approved, Denied.
// ("Pending upload" is NOT here on purpose: that only exists on the phone before sync.)
public class ReportStatus
{
    public int StatusId { get; set; }                // StatusID (PK)

    [MaxLength(50)]
    public string StatusName { get; set; } = "";

    public bool IsTerminal { get; set; }             // true = final, nothing happens after it
    public int DisplayOrder { get; set; }            // unique, >= 0
}
