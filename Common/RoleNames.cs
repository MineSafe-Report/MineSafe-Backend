namespace MineSafety.Api.Common;

// The four role names in ONE place, so a typo can't break a permission check.
// Used like:  [Authorize(Roles = RoleNames.Administrator)]
public static class RoleNames
{
    public const string Worker = "Worker";
    public const string Supervisor = "Supervisor";
    public const string SafetyOfficer = "SafetyOfficer";
    public const string Administrator = "Administrator";
}
