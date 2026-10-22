namespace MineSafety.Api.Entities;

// Maps to the AccountRoles table (database/schema/AccountRoles.sql).
// Each row means "this account has this role". When a role is removed the row is
// marked inactive instead of deleted, so the history is kept.
public class AccountRole
{
    public int AccountRoleId { get; set; }         // AccountRoleID (primary key)

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
    public int? AssignedBy { get; set; }           // AccountID of the administrator who assigned it
    public bool IsActive { get; set; } = true;
}
