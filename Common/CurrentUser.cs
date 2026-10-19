namespace MineSafety.Api.Common;

// Everything a service needs to know about WHO is asking, in one small object:
// their account ID, their current roles, and which locations they may see.
// Built once per request by AccessService.GetCurrentUserAsync.
public class CurrentUser
{
    public int AccountId { get; init; }
    public List<string> Roles { get; init; } = new();

    // null = may see every location (Safety Officer, Administrator)
    public List<int>? AllowedLocationIds { get; init; }

    public bool IsInRole(string role) => Roles.Contains(role);
    public bool SeesAllLocations => AllowedLocationIds == null;
    public bool CanSeeLocation(int locationId) => AllowedLocationIds == null || AllowedLocationIds.Contains(locationId);
}
