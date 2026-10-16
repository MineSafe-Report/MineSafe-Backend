using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Handles everything under /api/locations.
// The controller is the receptionist: it receives the request, passes it to the service,
// and picks the right status code. No business rules live here.
[Route("api/locations")]
[Authorize]   // every endpoint here needs a valid token...
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
    // Workers use this for the location dropdown. Admins use it for the locations screen.
    // Workers/Supervisors only get THEIR assigned locations; Safety Officers/Admins get all.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, [FromQuery] string? search = null)
    {
        // Only admins may see switched-off locations.
        if (!User.IsInRole(RoleNames.Administrator))
            includeInactive = false;

        var allowed = await _access.GetAllowedLocationIdsAsync(User);
        var locations = await _locations.GetAllAsync(includeInactive, search, allowed);
        return Ok(locations);                                        // 200
    }

    // GET /api/locations/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        // Outside your assigned locations? Answer 404, as if it doesn't exist (no data leakage).
        var allowed = await _access.GetAllowedLocationIdsAsync(User);
        if (allowed != null && !allowed.Contains(id))
            return NotFound(new { message = $"Location {id} was not found." });

        var result = await _locations.GetByIdAsync(id);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // POST /api/locations          body: { "locationName": "...", "locationCode": "...", "locationInfo": "..." }
    [HttpPost]
    [Authorize(Roles = RoleNames.Administrator)]   // ...and these need the Administrator role (else 403)
    public async Task<IActionResult> Create(CreateLocationRequest request)
    {
        var result = await _locations.CreateAsync(request);
        if (!result.Success)
            return ErrorResponse(result);

        // 201 Created, plus a "Location" header pointing at the new item.
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

    // DELETE /api/locations/5     (soft delete: sets IsActive = false)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Administrator)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _locations.DeleteAsync(id);
        return result.Success ? NoContent() : ErrorResponse(result);   // 204 = done, nothing to return
    }
}
