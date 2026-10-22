using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;

namespace MineSafety.Api.Services;

public interface IAccessService
{
    // Returns null when the user may see every location (Safety Officer, Administrator),
    // otherwise the list of location IDs assigned to them in AccountLocations.
    Task<List<int>?> GetAllowedLocationIdsAsync(ClaimsPrincipal user);

    // The account ID, current roles and allowed locations, bundled together for the services.
    Task<CurrentUser> GetCurrentUserAsync(ClaimsPrincipal user);
}

// Works out which locations a user may access. Keeping this in one place means a supervisor
// at Tunnel 7 can't read Stope 12B's data just by changing an ID in the URL.
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

    public async Task<CurrentUser> GetCurrentUserAsync(ClaimsPrincipal user)
    {
        return new CurrentUser
        {
            AccountId = int.Parse(user.FindFirst("sub")!.Value),
            // RoleRefresher has already replaced these with the roles currently in the database.
            Roles = user.FindAll("role").Select(c => c.Value).ToList(),
            AllowedLocationIds = await GetAllowedLocationIdsAsync(user)
        };
    }
}
