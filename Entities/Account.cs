using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the Accounts table (database/schema/Accounts.sql).
// Accounts has no RoleID column. Roles are linked through AccountRoles and locations through
// AccountLocations, so one account can have several of each.
public class Account
{
    public int AccountId { get; set; }             // AccountID (primary key)

    [MaxLength(50)]
    public string WorkerId { get; set; } = "";     // WorkerID, unique, used to log in

    [MaxLength(100)]
    public string FullName { get; set; } = "";

    [MaxLength(255)]
    public string Email { get; set; } = "";        // required and unique in SQL

    [MaxLength(255)]
    public string PasswordHash { get; set; } = ""; // only the hash is stored, never the password

    public bool IsActive { get; set; } = true;     // false = soft deleted
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // SQL Server changes this value on every update. If two people edit the same account at
    // the same time, the second save is rejected instead of silently overwriting the first.
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation properties to the account's role and location links.
    public List<AccountRole> AccountRoles { get; set; } = new();
    public List<AccountLocation> AccountLocations { get; set; } = new();
}
