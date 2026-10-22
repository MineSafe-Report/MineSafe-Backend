namespace MineSafety.Api.Entities;

// Matches database/schema/AccountLocations.sql
// One row = "this account works at / may see this location".
// This is what stops a supervisor at Tunnel 7 from seeing Stope 12B's reports.
public class AccountLocation
{
    // The SQL primary key column is called "AccountLocation" (no ID at the end).
    // The DbContext maps this property to that exact column name.
    public int AccountLocationId { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public bool IsDefault { get; set; }              // pre-selected in the app's dropdown
    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; }
    public int? AssignedBy { get; set; }
}
