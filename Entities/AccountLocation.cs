namespace MineSafety.Api.Entities;

// Maps to the AccountLocations table (database/schema/AccountLocations.sql).
// Each row means "this account works at, and may see, this location". This is what keeps a
// supervisor at Tunnel 7 from seeing the reports for Stope 12B.
public class AccountLocation
{
    // The SQL primary key column is called "AccountLocation" (without "ID").
    // MineSafetyDbContext maps this property to that column name.
    public int AccountLocationId { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public bool IsDefault { get; set; }            // pre-selected in the app's location dropdown
    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; }
    public int? AssignedBy { get; set; }
}
