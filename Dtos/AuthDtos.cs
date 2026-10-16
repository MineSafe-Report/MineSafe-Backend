using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Dtos;

// DTO = Data Transfer Object: the exact shape of the JSON that goes in or out.
// We never send entities directly (Account has a PasswordHash we must never expose).

// What the app SENDS to POST /api/auth/login
public class LoginRequest
{
    // Worker ID (e.g. W1001) or email address. Both work.
    [Required, MaxLength(255)]
    public string Login { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

// What the API SENDS BACK after a successful login
public class LoginResponse
{
    public string Token { get; set; } = "";          // the app stores this and sends it with every request
    public DateTime ExpiresAt { get; set; }          // when the app must log in again
    public CurrentUserDto User { get; set; } = new();
}

// Who is logged in. Returned by login and by GET /api/auth/me
public class CurrentUserDto
{
    public int AccountId { get; set; }
    public string WorkerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();             // the app uses these to show/hide screens
    public List<AssignedLocationDto> Locations { get; set; } = new();
}

// A location linked to an account (from AccountLocations)
public class AssignedLocationDto
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string LocationCode { get; set; } = "";
    public bool IsDefault { get; set; }
}
