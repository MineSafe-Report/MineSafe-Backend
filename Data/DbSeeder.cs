using Microsoft.AspNetCore.Identity;
using MineSafety.Api.Common;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Data;

// Puts starter data into an EMPTY database so we can test straight away:
// the four roles, the three report types, the statuses, some locations, and one test account per role.
// It only runs if the Roles table is empty, so it never overwrites real data.
public static class DbSeeder
{
    // Every test account uses this password. Development only.
    public const string TestPassword = "Password123!";

    public static void Seed(MineSafetyDbContext db)
    {
        if (db.Roles.Any()) return;

        var now = DateTime.UtcNow;

        var worker = new Role { RoleName = RoleNames.Worker, Description = "Underground worker who submits reports" };
        var supervisor = new Role { RoleName = RoleNames.Supervisor, Description = "Reviews reports for their area" };
        var safetyOfficer = new Role { RoleName = RoleNames.SafetyOfficer, Description = "Manages reports mine-wide" };
        var admin = new Role { RoleName = RoleNames.Administrator, Description = "Manages accounts and locations" };
        db.Roles.AddRange(worker, supervisor, safetyOfficer, admin);

        db.IncidentTypes.AddRange(
            new IncidentType { TypeName = "Hazard", TriggersImmediateAlert = false },
            new IncidentType { TypeName = "Incident", TriggersImmediateAlert = true },
            new IncidentType { TypeName = "Near Miss", TriggersImmediateAlert = false });

        db.ReportStatuses.AddRange(
            new ReportStatus { StatusName = "Pending Review", IsTerminal = false, DisplayOrder = 1 },
            new ReportStatus { StatusName = "Approved", IsTerminal = true, DisplayOrder = 2 },
            new ReportStatus { StatusName = "Denied", IsTerminal = true, DisplayOrder = 3 });

        db.Locations.AddRange(
            new Location { LocationName = "Shaft 1 Station", LocationCode = "S1-ST", LocationInfo = "Main shaft landing, level 1", CreatedAt = now },
            new Location { LocationName = "Tunnel 7", LocationCode = "T-7", LocationInfo = "Haulage tunnel, level 3", CreatedAt = now },
            new Location { LocationName = "Stope 12B", LocationCode = "ST-12B", LocationInfo = "Active stope, level 4", CreatedAt = now },
            new Location { LocationName = "Workshop", LocationCode = "WS-1", LocationInfo = "Underground vehicle workshop", CreatedAt = now });

        // Build each test account and hash its password the same way the real code does.
        var hasher = new PasswordHasher<Account>();
        Account Make(string workerId, string name, string email, Role role)
        {
            var a = new Account { WorkerId = workerId, FullName = name, Email = email, Role = role, CreatedAt = now };
            a.PasswordHash = hasher.HashPassword(a, TestPassword);
            return a;
        }

        db.Accounts.AddRange(
            Make("W1001", "Test Worker", "worker@mine.test", worker),
            Make("S2001", "Test Supervisor", "supervisor@mine.test", supervisor),
            Make("O3001", "Test Safety Officer", "officer@mine.test", safetyOfficer),
            Make("A9001", "Test Admin", "admin@mine.test", admin));

        db.SaveChanges();
    }
}
