using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the ReportStatuses table (database/schema/ReportStatuses.sql): Pending Review, Approved and Denied.
// "Pending upload" is not a status here because it only exists on the phone before the report syncs.
public class ReportStatus
{
    public int StatusId { get; set; }              // StatusID (primary key)

    [MaxLength(50)]
    public string StatusName { get; set; } = "";

    public bool IsTerminal { get; set; }           // true means final, the status won't change again
    public int DisplayOrder { get; set; }          // unique, 0 or higher
}
