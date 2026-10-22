using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the Locations table (database/schema/004_Locations.sql).
public class Location
{
    public int LocationId { get; set; }            // LocationID (primary key)

    [MaxLength(100)]
    public string LocationName { get; set; } = ""; // for example "Tunnel 7"

    [MaxLength(50)]
    public string LocationCode { get; set; } = ""; // for example "T-7", unique

    [MaxLength(255)]
    public string? LocationInfo { get; set; }

    public bool IsActive { get; set; } = true;     // false = soft deleted
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
