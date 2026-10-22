using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Dtos;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Services;

// The controller depends on this interface rather than on LocationService itself,
// so the implementation can change without the controller having to change.
public interface ILocationService
{
    // allowedLocationIds: null means no limit, otherwise only these IDs are returned.
    Task<List<LocationDto>> GetAllAsync(bool includeInactive, string? search, List<int>? allowedLocationIds);
    Task<ServiceResult<LocationDto>> GetByIdAsync(int id);
    Task<ServiceResult<LocationDto>> CreateAsync(CreateLocationRequest request);
    Task<ServiceResult<LocationDto>> UpdateAsync(int id, UpdateLocationRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}

// The business rules for locations. The other services (accounts and reports) follow the same structure.
public class LocationService : ILocationService
{
    private readonly MineSafetyDbContext _db;

    public LocationService(MineSafetyDbContext db)
    {
        _db = db;
    }

    // ---------- Read ----------

    public async Task<List<LocationDto>> GetAllAsync(bool includeInactive, string? search, List<int>? allowedLocationIds)
    {
        // Start with all locations and add the filters one by one.
        // The query only runs against the database at ToListAsync().
        IQueryable<Location> query = _db.Locations.AsNoTracking();

        if (!includeInactive)
            query = query.Where(l => l.IsActive);

        // Workers and Supervisors only get the locations assigned to them.
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

    public async Task<ServiceResult<LocationDto>> GetByIdAsync(int id)
    {
        var location = await _db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.LocationId == id);

        return location == null
            ? ServiceResult<LocationDto>.NotFound($"Location {id} was not found.")
            : ServiceResult<LocationDto>.Ok(LocationDto.FromEntity(location));
    }

    // ---------- Create ----------

    public async Task<ServiceResult<LocationDto>> CreateAsync(CreateLocationRequest request)
    {
        var code = request.LocationCode.Trim().ToUpper();

        // Location codes must be unique.
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

        _db.Locations.Add(location);
        await _db.SaveChangesAsync(); // SQL Server generates the LocationId here

        return ServiceResult<LocationDto>.Ok(LocationDto.FromEntity(location));
    }

    // ---------- Update ----------

    public async Task<ServiceResult<LocationDto>> UpdateAsync(int id, UpdateLocationRequest request)
    {
        // No AsNoTracking here, because EF needs to track this row to detect the changes.
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

        await _db.SaveChangesAsync(); // EF only updates the columns that changed

        return ServiceResult<LocationDto>.Ok(LocationDto.FromEntity(location));
    }

    // ---------- Delete (soft) ----------

    // Old reports must keep their location, so it is deactivated instead of deleted.
    // It disappears from the location dropdown, but existing reports still point to it.
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
