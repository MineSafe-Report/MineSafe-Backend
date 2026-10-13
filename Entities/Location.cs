using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches the "Locations" table in the D3 ERD.
public class Location
{
    public int LocationId { get; set; }

    [MaxLength(100)]
    public string LocationName { get; set; } = "";   // e.g. "Tunnel 7"

    [MaxLength(20)]
    public string LocationCode { get; set; } = "";   // e.g. "T-7" (must be unique)

    [MaxLength(500)]
    public string? LocationInfo { get; set; }

    public bool IsActive { get; set; } = true;       // soft delete flag

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
