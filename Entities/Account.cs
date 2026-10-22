using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches database/schema/Accounts.sql
// NOTE: there is NO RoleID on Accounts. Roles are linked through the AccountRoles table,
// and locations through AccountLocations, so one account can have several of each.
public class Account
{
    public int AccountId { get; set; }               // AccountID (PK)

    [MaxLength(50)]
    public string WorkerId { get; set; } = "";       // WorkerID, unique, used to log in

    [MaxLength(100)]
    public string FullName { get; set; } = "";

    [MaxLength(255)]
    public string Email { get; set; } = "";          // required + unique in SQL

    [MaxLength(255)]
    public string PasswordHash { get; set; } = "";   // never the real password, only its hash

    public bool IsActive { get; set; } = true;       // soft delete flag
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ROWVERSION: SQL Server changes this on every update. If two people edit the same
    // account at once, the second save is rejected instead of silently overwriting.
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation properties: let us go from an account to its role and location links in C#.
    public List<AccountRole> AccountRoles { get; set; } = new();
    public List<AccountLocation> AccountLocations { get; set; } = new();
}
