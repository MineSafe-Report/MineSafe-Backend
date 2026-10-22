namespace MineSafety.Api.Common;

// Who is making the request: their account ID, their current roles and the locations they may see.
// Built once per request by AccessService.GetCurrentUserAsync and passed to the services.
public class CurrentUser
{
    public int AccountId { get; init; }
    public List<string> Roles { get; init; } = new();

    // Null means the user may see every location (Safety Officer and Administrator).
    public List<int>? AllowedLocationIds { get; init; }

    public bool IsInRole(string role) => Roles.Contains(role);
    public bool SeesAllLocations => AllowedLocationIds == null;
    public bool CanSeeLocation(int locationId) => AllowedLocationIds == null || AllowedLocationIds.Contains(locationId);
}
