using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Handles everything under /api/auth
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    // POST /api/auth/login
    // Open to everyone (you can't have a token before you log in).
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        if (!result.Success)
            return Unauthorized(new { message = result.Message });   // 401

        return Ok(result.Value);                                     // 200 + token
    }

    // GET /api/auth/me
    // The app calls this when it opens, to check the saved token still works and who it belongs to.
    [HttpGet("me")]
    [Authorize]   // any logged-in user
    public async Task<IActionResult> Me()
    {
        var result = await _auth.GetCurrentUserAsync(CurrentAccountId);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // There is no logout endpoint on purpose: a JWT isn't stored on the server,
    // so "logging out" means the app deletes the token it saved. That's Banjo's side.
}
