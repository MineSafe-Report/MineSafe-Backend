using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// ENTITY = a C# class that mirrors one database table. One property = one column.
// Matches database/schema/001_Roles.sql
public class Role
{
    public int RoleId { get; set; }                  // RoleID (PK)

    [MaxLength(50)]
    public string RoleName { get; set; } = "";       // Worker, Supervisor, SafetyOfficer, Administrator

    [MaxLength(255)]
    public string? Description { get; set; }
}
