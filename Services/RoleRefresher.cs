using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Data;

namespace MineSafety.Api.Services;

// Runs on every request, right after the token's signature and expiry have been checked.
//
// A token is valid for 12 hours. If an administrator deactivates an account or removes a role
// at 10:00, a token issued at 08:00 would otherwise keep that role until 20:00. So instead of
// trusting the roles stored in the token, they are loaded again from the database:
//   - if the account is deactivated or has no active roles, the request is rejected with 401
//   - otherwise the token's roles are replaced with the current roles from AccountRoles
// This costs one small query per request, which is fine for a system of this size.
public static class RoleRefresher
{
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<MineSafetyDbContext>();

        var sub = context.Principal?.FindFirst("sub")?.Value;
        if (!int.TryParse(sub, out var accountId))
        {
            context.Fail("Token has no valid account ID.");
            return;
        }

        var currentRoles = await db.AccountRoles
            .AsNoTracking()
            .Where(ar => ar.AccountId == accountId && ar.IsActive && ar.Account.IsActive)
            .Select(ar => ar.Role.RoleName)
            .ToListAsync();

        if (currentRoles.Count == 0)
        {
            context.Fail("Account is deactivated or has no active roles.");
            return;
        }

        // Replace the token's role claims with the current roles.
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        foreach (var oldRole in identity.FindAll("role").ToList())
            identity.RemoveClaim(oldRole);
        foreach (var role in currentRoles)
            identity.AddClaim(new Claim("role", role));
    }
}
