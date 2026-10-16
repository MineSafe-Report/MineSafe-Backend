using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches database/schema/004_Locations.sql
public class Location
{
    public int LocationId { get; set; }              // LocationID (PK)

    [MaxLength(100)]
    public string LocationName { get; set; } = "";   // e.g. "Tunnel 7"

    [MaxLength(50)]
    public string LocationCode { get; set; } = "";   // e.g. "T-7", unique

    [MaxLength(255)]
    public string? LocationInfo { get; set; }

    public bool IsActive { get; set; } = true;       // soft delete flag
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
