using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches the "Accounts" table in the D3 ERD.
public class Account
{
    public int AccountId { get; set; }

    [MaxLength(30)]
    public string WorkerId { get; set; } = "";       // the ID printed on the worker's card, used to log in

    [MaxLength(150)]
    public string FullName { get; set; } = "";

    [MaxLength(200)]
    public string? Email { get; set; }

    // We NEVER store the real password, only a one-way hash of it.
    public string PasswordHash { get; set; } = "";

    public int RoleId { get; set; }                  // FK to Roles
    public Role Role { get; set; } = null!;          // navigation: account.Role.RoleName

    // Soft delete: we never remove an account (old reports point to it), we just switch it off.
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Concurrency protection (from the ERD). SQL Server changes this value on every update,
    // so if two people edit the same account at once, the second save is rejected.
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
