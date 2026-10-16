using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;

namespace MineSafety.Api.Services;

public interface IAccessService
{
    // null  = this user may see EVERY location (Safety Officer, Administrator)
    // list  = this user may only see these location IDs (from AccountLocations)
    Task<List<int>?> GetAllowedLocationIdsAsync(ClaimsPrincipal user);
}

// Location-scoped access in ONE place. Reports (next step) will use the same method, so a
// Supervisor assigned to Tunnel 7 can never read Stope 12B's data by changing the URL.
public class AccessService : IAccessService
{
    private readonly MineSafetyDbContext _db;

    public AccessService(MineSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<List<int>?> GetAllowedLocationIdsAsync(ClaimsPrincipal user)
    {
        if (RoleNames.SeeAllLocations.Any(user.IsInRole))
            return null;

        var accountId = int.Parse(user.FindFirst("sub")!.Value);

        return await _db.AccountLocations
            .AsNoTracking()
            .Where(al => al.AccountId == accountId && al.IsActive && al.Location.IsActive)
            .Select(al => al.LocationId)
            .ToListAsync();
    }
}
