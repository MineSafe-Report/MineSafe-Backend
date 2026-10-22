using System.ComponentModel.DataAnnotations;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Dtos;

// A location as returned by the API.
public class LocationDto
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string LocationCode { get; set; } = "";
    public string? LocationInfo { get; set; }
    public bool IsActive { get; set; }

    // Copies a database row into the JSON shape.
    public static LocationDto FromEntity(Location l) => new()
    {
        LocationId = l.LocationId,
        LocationName = l.LocationName,
        LocationCode = l.LocationCode,
        LocationInfo = l.LocationInfo,
        IsActive = l.IsActive
    };
}

// Body for POST /api/locations. The validation attributes match the SQL column sizes,
// and ASP.NET returns 400 automatically if any of them fail.
public class CreateLocationRequest
{
    [Required, MaxLength(100)]
    public string LocationName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LocationCode { get; set; } = "";

    [MaxLength(255)]
    public string? LocationInfo { get; set; }
}

// Body for PUT /api/locations/5. Setting IsActive back to true reactivates a deleted location.
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
