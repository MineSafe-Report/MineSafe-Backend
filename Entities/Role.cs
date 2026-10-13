using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// ENTITY = a C# class that mirrors one database table.
// One property = one column. EF Core uses these to read and write rows.
// This one matches the "Roles" table in the D3 ERD.
public class Role
{
    public int RoleId { get; set; }                 // PK (EF treats "<ClassName>Id" as the primary key)

    [MaxLength(50)]
    public string RoleName { get; set; } = "";       // Worker, Supervisor, SafetyOfficer, Administrator

    [MaxLength(250)]
    public string? Description { get; set; }

    // Navigation property: lets us go from a role to all its accounts in C#.
    public List<Account> Accounts { get; set; } = new();
}
