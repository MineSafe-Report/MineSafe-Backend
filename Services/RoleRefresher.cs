using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Data;

namespace MineSafety.Api.Services;

// Runs on EVERY request, right after the token's signature and expiry have been checked.
//
// Problem it solves: a token lasts 12 hours. If an admin deactivates an account or removes
// a role at 10:00, a token issued at 08:00 would still say "Supervisor" until 20:00.
// So instead of trusting the roles written inside the token, we look them up fresh:
//   - account deactivated, or no active roles left  ->  request rejected with 401
//   - otherwise the user's roles are replaced with their CURRENT roles from AccountRoles
// Cost: one small database query per request, which is fine for this system.
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

        // Swap the token's role claims for the current ones.
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        foreach (var oldRole in identity.FindAll("role").ToList())
            identity.RemoveClaim(oldRole);
        foreach (var role in currentRoles)
            identity.AddClaim(new Claim("role", role));
    }
}
