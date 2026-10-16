using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Dtos;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Services;

// The interface is the "menu" of what this service can do.
// The controller only knows about this menu, not the class behind it,
// so the class can change without the controller changing.
public interface ILocationService
{
    // allowedLocationIds: null = no limit, otherwise only these IDs are returned.
    Task<List<LocationDto>> GetAllAsync(bool includeInactive, string? search, List<int>? allowedLocationIds);
    Task<ServiceResult<LocationDto>> GetByIdAsync(int id);
    Task<ServiceResult<LocationDto>> CreateAsync(CreateLocationRequest request);
    Task<ServiceResult<LocationDto>> UpdateAsync(int id, UpdateLocationRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}

// All the rules for locations live here. This is the TEMPLATE for every other module:
// Accounts, and later Reports, follow exactly the same shape.
public class LocationService : ILocationService
{
    private readonly MineSafetyDbContext _db;

    public LocationService(MineSafetyDbContext db)
    {
        _db = db;
    }

    // READ (list)
    public async Task<List<LocationDto>> GetAllAsync(bool includeInactive, string? search, List<int>? allowedLocationIds)
    {
        // Start with "all locations" and add filters step by step.
        // Nothing is sent to the database until ToListAsync() at the end.
        IQueryable<Location> query = _db.Locations.AsNoTracking();

        if (!includeInactive)
            query = query.Where(l => l.IsActive);

        // Location scoping: Workers/Supervisors only get the locations assigned to them.
        if (allowedLocationIds != null)
            query = query.Where(l => allowedLocationIds.Contains(l.LocationId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(l => l.LocationName.Contains(s) || l.LocationCode.Contains(s));
        }

        var locations = await query.OrderBy(l => l.LocationName).ToListAsync();
        return locations.Select(LocationDto.FromEntity).ToList();
    }

    // READ (one)
    public async Task<ServiceResult<LocationDto>> GetByIdAsync(int id)
    {
        var location = await _db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.LocationId == id);

        return location == null
            ? ServiceResult<LocationDto>.NotFound($"Location {id} was not found.")
            : ServiceResult<LocationDto>.Ok(LocationDto.FromEntity(location));
    }

    // CREATE
    public async Task<ServiceResult<LocationDto>> CreateAsync(CreateLocationRequest request)
    {
        var code = request.LocationCode.Trim().ToUpper();

        // Business rule: location codes must be unique.
        if (await _db.Locations.AnyAsync(l => l.LocationCode == code))
            return ServiceResult<LocationDto>.Conflict($"Location code '{code}' is already in use.");

        var location = new Location
        {
            LocationName = request.LocationName.Trim(),
            LocationCode = code,
            LocationInfo = request.LocationInfo?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Locations.Add(location);       // tell EF about the new row
        await _db.SaveChangesAsync();      // EF writes the INSERT; SQL Server fills in LocationId

        return ServiceResult<LocationDto>.Ok(LocationDto.FromEntity(location));
    }

    // UPDATE
    public async Task<ServiceResult<LocationDto>> UpdateAsync(int id, UpdateLocationRequest request)
    {
        // No AsNoTracking here: we WANT EF to track this row so it notices our changes.
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.LocationId == id);
        if (location == null)
            return ServiceResult<LocationDto>.NotFound($"Location {id} was not found.");

        var code = request.LocationCode.Trim().ToUpper();
        if (await _db.Locations.AnyAsync(l => l.LocationCode == code && l.LocationId != id))
            return ServiceResult<LocationDto>.Conflict($"Location code '{code}' is already in use.");

        location.LocationName = request.LocationName.Trim();
        location.LocationCode = code;
        location.LocationInfo = request.LocationInfo?.Trim();
        location.IsActive = request.IsActive;
        location.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();      // EF sees which properties changed and writes the UPDATE

        return ServiceResult<LocationDto>.Ok(LocationDto.FromEntity(location));
    }

    // DELETE (soft): D3 says old reports must keep their location, so we switch it off instead.
    // It disappears from the worker's dropdown but every old report still points to it.
    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.LocationId == id);
        if (location == null)
            return ServiceResult<bool>.NotFound($"Location {id} was not found.");

        location.IsActive = false;
        location.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }
}
