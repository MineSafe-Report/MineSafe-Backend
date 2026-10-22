using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Endpoints under /api/locations.
// Like every controller, this one only receives the request, passes it to the service and
// returns the right status code. The business rules live in LocationService.
[Route("api/locations")]
[Authorize]
public class LocationsController : ApiControllerBase
{
    private readonly ILocationService _locations;
    private readonly IAccessService _access;

    public LocationsController(ILocationService locations, IAccessService access)
    {
        _locations = locations;
        _access = access;
    }

    // GET /api/locations?includeInactive=false&search=tunnel
    // Used for the location dropdown and the admin locations screen.
    // Workers and Supervisors only get their assigned locations, Safety Officers and Administrators get all of them.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, [FromQuery] string? search = null)
    {
        // Only administrators can see deactivated locations.
        if (!User.IsInRole(RoleNames.Administrator))
            includeInactive = false;

        var allowed = await _access.GetAllowedLocationIdsAsync(User);
        var locations = await _locations.GetAllAsync(includeInactive, search, allowed);
        return Ok(locations);
    }

    // GET /api/locations/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        // A location outside your assignments returns 404, so you can't tell that it exists.
        var allowed = await _access.GetAllowedLocationIdsAsync(User);
        if (allowed != null && !allowed.Contains(id))
            return NotFound(new { message = $"Location {id} was not found." });

        var result = await _locations.GetByIdAsync(id);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // POST /api/locations
    // Body: { "locationName": "...", "locationCode": "...", "locationInfo": "..." }
    [HttpPost]
    [Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Create(CreateLocationRequest request)
    {
        var result = await _locations.CreateAsync(request);
        if (!result.Success)
            return ErrorResponse(result);

        // 201 Created, with a Location header pointing to the new item.
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.LocationId }, result.Value);
    }

    // PUT /api/locations/5
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Update(int id, UpdateLocationRequest request)
    {
        var result = await _locations.UpdateAsync(id, request);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // DELETE /api/locations/5
    // Soft delete: the location is marked inactive instead of being removed.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _locations.DeleteAsync(id);
        return result.Success ? NoContent() : ErrorResponse(result);
    }
}
