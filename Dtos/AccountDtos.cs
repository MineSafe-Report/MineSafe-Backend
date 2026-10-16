using System.ComponentModel.DataAnnotations;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Dtos;

// What the API sends back for an account. Notice: NO password or hash in here.
public class AccountDto
{
    public int AccountId { get; set; }
    public string WorkerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RoleDto> Roles { get; set; } = new();                    // active roles only
    public List<AssignedLocationDto> Locations { get; set; } = new();    // active locations only

    // Needs AccountRoles.Role and AccountLocations.Location loaded (.Include in the service).
    public static AccountDto FromEntity(Account a) => new()
    {
        AccountId = a.AccountId,
        WorkerId = a.WorkerId,
        FullName = a.FullName,
        Email = a.Email,
        IsActive = a.IsActive,
        CreatedAt = a.CreatedAt,
        Roles = a.AccountRoles
            .Where(ar => ar.IsActive)
            .Select(ar => RoleDto.FromEntity(ar.Role))
            .ToList(),
        Locations = a.AccountLocations
            .Where(al => al.IsActive)
            .Select(al => new AssignedLocationDto
            {
                LocationId = al.LocationId,
                LocationName = al.Location.LocationName,
                LocationCode = al.Location.LocationCode,
                IsDefault = al.IsDefault
            })
            .ToList()
    };
}

// Sizes match database/schema/005_Accounts.sql
public class CreateAccountRequest
{
    [Required, MaxLength(50)]
    public string WorkerId { get; set; } = "";

    [Required, MaxLength(100)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = "";

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = "";       // hashed before saving, never stored as typed

    // At least one role. An account can have more than one (e.g. Supervisor + SafetyOfficer).
    [Required, MinLength(1)]
    public List<int> RoleIds { get; set; } = new();

    // Locations this person works at. Required for Workers and Supervisors.
    public List<int> LocationIds { get; set; } = new();

    // Which of LocationIds is pre-selected in the app. Defaults to the first one.
    public int? DefaultLocationId { get; set; }
}

// The FULL new state of the account. Roles/locations not in the lists get switched off.
public class UpdateAccountRequest
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = "";

    [Required, MinLength(1)]
    public List<int> RoleIds { get; set; } = new();

    public List<int> LocationIds { get; set; } = new();

    public int? DefaultLocationId { get; set; }

    public bool IsActive { get; set; } = true;

    // Optional. Fill it in only when the admin is resetting this person's password.
    [MinLength(8), MaxLength(100)]
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
