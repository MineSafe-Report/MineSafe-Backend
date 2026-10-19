namespace MineSafety.Api.Common;

// The apps use fixed CODES ("NEAR_MISS", "PENDING_REVIEW"), the database keeps nice display
// NAMES ("Near Miss", "Pending Review"). This turns a name into its code, so both stay as they are.
public static class Codes
{
    public static string From(string name) => name.Trim().ToUpperInvariant().Replace(' ', '_');
}
