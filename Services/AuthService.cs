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
    // and ASP.NET hands them to us here automatically.
    public AuthService(MineSafetyDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var login = request.Login.Trim();

        // Find the account by worker ID OR email, and load its role and location links.
        var account = await LoadAccount()
            .FirstOrDefaultAsync(a => a.WorkerId == login || a.Email == login.ToLower());

        // Same vague message for every failure, so nobody can use the login screen
        // to find out which worker IDs exist.
        const string failMessage = "Invalid login details.";

        if (account == null || !account.IsActive)
            return ServiceResult<LoginResponse>.BadRequest(failMessage);

        // Hash what they typed and compare it with the stored hash.
        var check = _hasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
        if (check == PasswordVerificationResult.Failed)
            return ServiceResult<LoginResponse>.BadRequest(failMessage);

        var user = ToCurrentUser(account);
        if (user.Roles.Count == 0)
            return ServiceResult<LoginResponse>.BadRequest("This account has no active role. Contact an administrator.");

        var (token, expiresAt) = _tokens.CreateToken(account, user.Roles);

        return ServiceResult<LoginResponse>.Ok(new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = user
        });
    }

    public async Task<ServiceResult<CurrentUserDto>> GetCurrentUserAsync(int accountId)
    {
        var account = await LoadAccount().AsNoTracking()
            .FirstOrDefaultAsync(a => a.AccountId == accountId && a.IsActive);

        return account == null
            ? ServiceResult<CurrentUserDto>.NotFound("Account not found or deactivated.")
            : ServiceResult<CurrentUserDto>.Ok(ToCurrentUser(account));
    }

    // Accounts together with their role links (+ role names) and location links (+ locations).
    private IQueryable<Account> LoadAccount() => _db.Accounts
        .Include(a => a.AccountRoles).ThenInclude(ar => ar.Role)
        .Include(a => a.AccountLocations).ThenInclude(al => al.Location);

    // Only ACTIVE role and location links count. Inactive ones are history, not access.
    private static CurrentUserDto ToCurrentUser(Account a) => new()
    {
        AccountId = a.AccountId,
        WorkerId = a.WorkerId,
        FullName = a.FullName,
        Email = a.Email,
        Roles = a.AccountRoles.Where(ar => ar.IsActive).Select(ar => ar.Role.RoleName).ToList(),
        Locations = a.AccountLocations
            .Where(al => al.IsActive && al.Location.IsActive)
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
