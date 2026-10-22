namespace MineSafety.Api.Entities;

// Matches database/schema/AccountRoles.sql
// A "link" table: one row = "this account has this role".
// Rows are switched off (IsActive = false) instead of deleted, so history is kept.
public class AccountRole
{
    public int AccountRoleId { get; set; }           // AccountRoleID (PK)

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
    public int? AssignedBy { get; set; }             // AccountID of the admin who assigned it
    public bool IsActive { get; set; } = true;
}
