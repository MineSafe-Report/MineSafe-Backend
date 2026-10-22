namespace MineSafety.Api.Common;

// The database stores display names like "Near Miss" and "Pending Review", while the apps work
// with fixed codes like "NEAR_MISS" and "PENDING_REVIEW". This converts a name into its code.
public static class Codes
{
    public static string From(string name) => name.Trim().ToUpperInvariant().Replace(' ', '_');
}
