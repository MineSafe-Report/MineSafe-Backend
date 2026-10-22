using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the Roles table (database/schema/Roles.sql).
// Each entity class mirrors one database table, with one property per column.
public class Role
{
    public int RoleId { get; set; }                // RoleID (primary key)

    [MaxLength(50)]
    public string RoleName { get; set; } = "";     // Worker, Supervisor, SafetyOfficer or Administrator

    [MaxLength(255)]
    public string? Description { get; set; }
}
