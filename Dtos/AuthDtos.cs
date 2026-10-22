using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Dtos;

// DTOs (data transfer objects) describe the exact JSON that goes in and out of the API.
// Entities are never returned directly, because Account contains the password hash.

// Body for POST /api/auth/login.
public class LoginRequest
{
    // Either the worker ID (for example W1001) or the email address.
    [Required, MaxLength(255)]
    public string Login { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

// Returned after a successful login.
public class LoginResponse
{
    public string Token { get; set; } = "";   // the app saves this and sends it with every request
    public DateTime ExpiresAt { get; set; }   // after this the user has to log in again
    public CurrentUserDto User { get; set; } = new();
}

// The logged-in user. Returned by the login endpoint and by GET /api/auth/me.
public class CurrentUserDto
{
    public int AccountId { get; set; }
    public string WorkerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new(); // the app uses these to decide which screens to show
    public List<AssignedLocationDto> Locations { get; set; } = new();
}

// A location assigned to an account (from AccountLocations).
public class AssignedLocationDto
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = "";
    public string LocationCode { get; set; } = "";
    public bool IsDefault { get; set; }
}
