namespace MineSafety.Api.Common;

// The role names used in permission checks. They must match the RoleName values in the Roles table.
public static class RoleNames
{
    public const string Worker = "Worker";
    public const string Supervisor = "Supervisor";
    public const string SafetyOfficer = "SafetyOfficer";
    public const string Administrator = "Administrator";

    // Roles that can see data from every location. Everyone else only sees the
    // locations assigned to them in AccountLocations.
    public static readonly string[] SeeAllLocations = { SafetyOfficer, Administrator };
}
