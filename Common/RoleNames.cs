namespace MineSafety.Api.Common;

// The four role names in ONE place, so a typo can't break a permission check.
// These must match the RoleName values in the Roles table.
public static class RoleNames
{
    public const string Worker = "Worker";
    public const string Supervisor = "Supervisor";
    public const string SafetyOfficer = "SafetyOfficer";
    public const string Administrator = "Administrator";

    // Roles that may see EVERY location's data. Everyone else is limited to the
    // locations assigned to them in AccountLocations.
    public static readonly string[] SeeAllLocations = { SafetyOfficer, Administrator };
}
