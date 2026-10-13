using System.ComponentModel.DataAnnotations;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Dtos;

// What the API sends back for an account. Notice: NO password or hash in here.
public class AccountDto
{
    public int AccountId { get; set; }
    public string WorkerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Needs the Role loaded (we use .Include(a => a.Role) in the service).
    public static AccountDto FromEntity(Account a) => new()
    {
        AccountId = a.AccountId,
        WorkerId = a.WorkerId,
        FullName = a.FullName,
        Email = a.Email,
        RoleId = a.RoleId,
        RoleName = a.Role?.RoleName ?? "",
        IsActive = a.IsActive,
        CreatedAt = a.CreatedAt
    };
}

public class CreateAccountRequest
{
    [Required, MaxLength(30)]
    public string WorkerId { get; set; } = "";

    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }

    [Required, MinLength(8)]
    public string Password { get; set; } = "";       // hashed before saving, never stored as typed

    [Required]
    public int RoleId { get; set; }
}

public class UpdateAccountRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = "";

    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }

    [Required]
    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    // Optional. Fill it in only when the admin is resetting this person's password.
    [MinLength(8)]
    public string? NewPassword { get; set; }
}

// For the role dropdown on the "create account" screen
public class RoleDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string? Description { get; set; }

    public static RoleDto FromEntity(Role r) => new()
    {
        RoleId = r.RoleId,
        RoleName = r.RoleName,
        Description = r.Description
    };
}
