using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Dtos;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Services;

public interface IAccountService
{
    Task<List<AccountDto>> GetAllAsync(string? search, int? roleId, bool includeInactive);
    Task<ServiceResult<AccountDto>> GetByIdAsync(int id);
    Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequest request);
    Task<ServiceResult<AccountDto>> UpdateAsync(int id, UpdateAccountRequest request, int currentAccountId);
    Task<ServiceResult<bool>> DeleteAsync(int id, int currentAccountId);
    Task<List<RoleDto>> GetRolesAsync();
}

// Same shape as LocationService, plus password hashing and role checks.
public class AccountService : IAccountService
{
    private readonly MineSafetyDbContext _db;
    private readonly PasswordHasher<Account> _hasher = new();

    public AccountService(MineSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<List<AccountDto>> GetAllAsync(string? search, int? roleId, bool includeInactive)
    {
        IQueryable<Account> query = _db.Accounts.AsNoTracking().Include(a => a.Role);

        if (!includeInactive)
            query = query.Where(a => a.IsActive);

        if (roleId.HasValue)
            query = query.Where(a => a.RoleId == roleId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.FullName.Contains(s) || a.WorkerId.Contains(s)
                                     || (a.Email != null && a.Email.Contains(s)));
        }

        var accounts = await query.OrderBy(a => a.FullName).ToListAsync();
        return accounts.Select(AccountDto.FromEntity).ToList();
    }

    public async Task<ServiceResult<AccountDto>> GetByIdAsync(int id)
    {
        var account = await _db.Accounts.AsNoTracking().Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.AccountId == id);

        return account == null
            ? ServiceResult<AccountDto>.NotFound($"Account {id} was not found.")
            : ServiceResult<AccountDto>.Ok(AccountDto.FromEntity(account));
    }

    public async Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequest request)
    {
        var workerId = request.WorkerId.Trim().ToUpper();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower();

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == request.RoleId);
        if (role == null)
            return ServiceResult<AccountDto>.BadRequest($"Role {request.RoleId} does not exist.");

        if (await _db.Accounts.AnyAsync(a => a.WorkerId == workerId))
            return ServiceResult<AccountDto>.Conflict($"Worker ID '{workerId}' already has an account.");

        if (email != null && await _db.Accounts.AnyAsync(a => a.Email == email))
            return ServiceResult<AccountDto>.Conflict($"Email '{email}' is already in use.");

        var account = new Account
        {
            WorkerId = workerId,
            FullName = request.FullName.Trim(),
            Email = email,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        // Turn the typed password into a hash. The typed password is never saved anywhere.
        account.PasswordHash = _hasher.HashPassword(account, request.Password);

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        return ServiceResult<AccountDto>.Ok(AccountDto.FromEntity(account));
    }

    public async Task<ServiceResult<AccountDto>> UpdateAsync(int id, UpdateAccountRequest request, int currentAccountId)
    {
        var account = await _db.Accounts.Include(a => a.Role).FirstOrDefaultAsync(a => a.AccountId == id);
        if (account == null)
            return ServiceResult<AccountDto>.NotFound($"Account {id} was not found.");

        // Safety rule: an admin can't lock themselves out by deactivating their own account.
        if (id == currentAccountId && !request.IsActive)
            return ServiceResult<AccountDto>.BadRequest("You can't deactivate your own account.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == request.RoleId);
        if (role == null)
            return ServiceResult<AccountDto>.BadRequest($"Role {request.RoleId} does not exist.");

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower();
        if (email != null && await _db.Accounts.AnyAsync(a => a.Email == email && a.AccountId != id))
            return ServiceResult<AccountDto>.Conflict($"Email '{email}' is already in use.");

        account.FullName = request.FullName.Trim();
        account.Email = email;
        account.Role = role;
        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;

        // Admin password reset: only if a new password was sent.
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
            account.PasswordHash = _hasher.HashPassword(account, request.NewPassword);

        await _db.SaveChangesAsync();

        return ServiceResult<AccountDto>.Ok(AccountDto.FromEntity(account));
    }

    // DELETE (soft): accounts linked to old reports must stay, so we deactivate.
    // A deactivated account can't log in any more (AuthService checks IsActive).
    public async Task<ServiceResult<bool>> DeleteAsync(int id, int currentAccountId)
    {
        if (id == currentAccountId)
            return ServiceResult<bool>.BadRequest("You can't delete your own account.");

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == id);
        if (account == null)
            return ServiceResult<bool>.NotFound($"Account {id} was not found.");

        account.IsActive = false;
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<List<RoleDto>> GetRolesAsync()
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.RoleId).ToListAsync();
        return roles.Select(RoleDto.FromEntity).ToList();
    }
}
