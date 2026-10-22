using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Endpoints under /api/auth.
// There is no logout endpoint because the server doesn't store tokens.
// Logging out simply means the app deletes the token it saved.
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    // POST /api/auth/login
    // Open to everyone, since nobody has a token before logging in.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        if (!result.Success)
            return Unauthorized(new { code = "INVALID_LOGIN", message = result.Message }); // 401

        return Ok(result.Value); // 200 with the token
    }

    // GET /api/auth/me
    // The app calls this on start-up to check that its saved token still works.
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var result = await _auth.GetCurrentUserAsync(CurrentAccountId);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }
}
