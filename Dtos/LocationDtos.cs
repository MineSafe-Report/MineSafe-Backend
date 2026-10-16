using System.ComponentModel.DataAnnotations;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Dtos;

// What the API sends back for a location
public class LocationDto
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string LocationCode { get; set; } = "";
    public string? LocationInfo { get; set; }
    public bool IsActive { get; set; }

    // Copies an entity (database row) into a DTO (JSON shape).
    public static LocationDto FromEntity(Location l) => new()
    {
        LocationId = l.LocationId,
        LocationName = l.LocationName,
        LocationCode = l.LocationCode,
        LocationInfo = l.LocationInfo,
        IsActive = l.IsActive
    };
}

// What the app sends to CREATE a location. The [attributes] are validation rules that match
// the SQL column sizes: break them and ASP.NET returns 400 before our code even runs.
public class CreateLocationRequest
{
    [Required, MaxLength(100)]
    public string LocationName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LocationCode { get; set; } = "";

    [MaxLength(255)]
    public string? LocationInfo { get; set; }
}

// What the app sends to UPDATE a location. Setting IsActive = true reactivates a deleted one.
public class UpdateLocationRequest
{
    [Required, MaxLength(100)]
    public string LocationName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LocationCode { get; set; } = "";

    [MaxLength(255)]
    public string? LocationInfo { get; set; }

    public bool IsActive { get; set; } = true;
}
