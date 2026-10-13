using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Dtos;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Services;

public interface IAuthService
{
    Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ServiceResult<CurrentUserDto>> GetCurrentUserAsync(int accountId);
}

// Checks login details and hands out tokens.
public class AuthService : IAuthService
{
    private readonly MineSafetyDbContext _db;
    private readonly ITokenService _tokens;
    private readonly PasswordHasher<Account> _hasher = new();

    // The DbContext and TokenService are "injected": Program.cs registered them,
    // and ASP.NET hands them to us here automatically. That's dependency injection.
    public AuthService(MineSafetyDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var login = request.Login.Trim();

        // Find the account by worker ID OR email, and load its Role at the same time.
        var account = await _db.Accounts
            .Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.WorkerId == login || a.Email == login);

        // Same vague message for every failure, so nobody can use the login screen
        // to find out which worker IDs exist.
        const string failMessage = "Invalid login details.";

        if (account == null || !account.IsActive)
            return ServiceResult<LoginResponse>.BadRequest(failMessage);

        // Hash what they typed and compare it with the stored hash.
        var check = _hasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
        if (check == PasswordVerificationResult.Failed)
            return ServiceResult<LoginResponse>.BadRequest(failMessage);

        var (token, expiresAt) = _tokens.CreateToken(account, account.Role.RoleName);

        return ServiceResult<LoginResponse>.Ok(new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = ToCurrentUser(account)
        });
    }

    public async Task<ServiceResult<CurrentUserDto>> GetCurrentUserAsync(int accountId)
    {
        var account = await _db.Accounts
            .AsNoTracking()                          // read-only query: a bit faster, nothing will be saved
            .Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.AccountId == accountId && a.IsActive);

        return account == null
            ? ServiceResult<CurrentUserDto>.NotFound("Account not found or deactivated.")
            : ServiceResult<CurrentUserDto>.Ok(ToCurrentUser(account));
    }

    private static CurrentUserDto ToCurrentUser(Account a) => new()
    {
        AccountId = a.AccountId,
        WorkerId = a.WorkerId,
        FullName = a.FullName,
        Role = a.Role.RoleName
    };
}
