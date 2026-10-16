using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Data;

// DEVELOPMENT ONLY. Fills an EMPTY database (already created by Michael's SQL scripts)
// with test data so we can log in and test straight away.
//
// It never creates tables and never overwrites anything: each table is only filled if it
// has no rows yet. So if Michael's seed scripts have already filled Roles, we skip Roles.
//
// Why test accounts are seeded here and not in SQL: passwords must be hashed by the same
// PasswordHasher the login code uses. A hash typed into a SQL file wouldn't match.
public static class DbSeeder
{
    public const string TestPassword = "Password123!";

    public static void Seed(MineSafetyDbContext db)
    {
        var now = DateTime.UtcNow;

        if (!db.Roles.Any())
        {
            db.Roles.AddRange(
                new Role { RoleName = RoleNames.Worker, Description = "Underground worker who submits reports" },
                new Role { RoleName = RoleNames.Supervisor, Description = "Reviews reports for their assigned locations" },
                new Role { RoleName = RoleNames.SafetyOfficer, Description = "Manages reports mine-wide" },
                new Role { RoleName = RoleNames.Administrator, Description = "Manages accounts and locations" });
            db.SaveChanges();
        }

        if (!db.IncidentTypes.Any())
        {
            db.IncidentTypes.AddRange(
                new IncidentType { TypeName = "Hazard", TriggersImmediateAlert = false },
                new IncidentType { TypeName = "Incident", TriggersImmediateAlert = true },
                new IncidentType { TypeName = "Near Miss", TriggersImmediateAlert = false });
            db.SaveChanges();
        }

        if (!db.ReportStatuses.Any())
        {
            db.ReportStatuses.AddRange(
                new ReportStatus { StatusName = "Pending Review", IsTerminal = false, DisplayOrder = 1 },
                new ReportStatus { StatusName = "Approved", IsTerminal = true, DisplayOrder = 2 },
                new ReportStatus { StatusName = "Denied", IsTerminal = true, DisplayOrder = 3 });
            db.SaveChanges();
        }

        if (!db.Locations.Any())
        {
            db.Locations.AddRange(
                new Location { LocationName = "Shaft 1 Station", LocationCode = "S1-ST", LocationInfo = "Main shaft landing, level 1", CreatedAt = now },
                new Location { LocationName = "Tunnel 7", LocationCode = "T-7", LocationInfo = "Haulage tunnel, level 3", CreatedAt = now },
                new Location { LocationName = "Stope 12B", LocationCode = "ST-12B", LocationInfo = "Active stope, level 4", CreatedAt = now },
                new Location { LocationName = "Workshop", LocationCode = "WS-1", LocationInfo = "Underground vehicle workshop", CreatedAt = now });
            db.SaveChanges();
        }

        if (!db.Accounts.Any())
        {
            var role = db.Roles.ToDictionary(r => r.RoleName, r => r.RoleId);
            var loc = db.Locations.ToDictionary(l => l.LocationCode, l => l.LocationId);
            var hasher = new PasswordHasher<Account>();

            Account Make(string workerId, string name, string email, string roleName, params string[] locationCodes)
            {
                var a = new Account { WorkerId = workerId, FullName = name, Email = email, IsActive = true, CreatedAt = now };
                a.PasswordHash = hasher.HashPassword(a, TestPassword);
                a.AccountRoles.Add(new AccountRole { RoleId = role[roleName], AssignedAt = now, IsActive = true });
                for (var i = 0; i < locationCodes.Length; i++)
                    a.AccountLocations.Add(new AccountLocation
                    {
                        LocationId = loc[locationCodes[i]],
                        IsDefault = i == 0,                 // first one listed is the default
                        AssignedAt = now,
                        IsActive = true
                    });
                return a;
            }

            db.Accounts.AddRange(
                Make("W1001", "Test Worker", "worker@mine.test", RoleNames.Worker, "T-7", "ST-12B"),
                Make("S2001", "Test Supervisor", "supervisor@mine.test", RoleNames.Supervisor, "T-7", "ST-12B"),
                // A second supervisor at DIFFERENT locations, to prove location scoping works.
                Make("S2002", "Other Supervisor", "supervisor2@mine.test", RoleNames.Supervisor, "S1-ST", "WS-1"),
                Make("O3001", "Test Safety Officer", "officer@mine.test", RoleNames.SafetyOfficer),
                Make("A9001", "Test Admin", "admin@mine.test", RoleNames.Administrator));
            db.SaveChanges();
        }
    }
}
