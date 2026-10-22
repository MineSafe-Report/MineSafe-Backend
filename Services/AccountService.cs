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
    Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequest request, int adminAccountId);
    Task<ServiceResult<AccountDto>> UpdateAsync(int id, UpdateAccountRequest request, int adminAccountId);
    Task<ServiceResult<bool>> DeleteAsync(int id, int adminAccountId);
    Task<List<RoleDto>> GetRolesAsync();
}

// Creating, reading, updating and deactivating accounts, together with their
// role links (AccountRoles) and location links (AccountLocations).
public class AccountService : IAccountService
{
    private readonly MineSafetyDbContext _db;
    private readonly PasswordHasher<Account> _hasher = new();

    public AccountService(MineSafetyDbContext db)
    {
        _db = db;
    }

    // Accounts with their roles and locations loaded, which AccountDto needs.
    private IQueryable<Account> AccountsWithLinks() => _db.Accounts
        .Include(a => a.AccountRoles).ThenInclude(ar => ar.Role)
        .Include(a => a.AccountLocations).ThenInclude(al => al.Location);

    // ---------- Read ----------

    public async Task<List<AccountDto>> GetAllAsync(string? search, int? roleId, bool includeInactive)
    {
        IQueryable<Account> query = AccountsWithLinks().AsNoTracking();

        if (!includeInactive)
            query = query.Where(a => a.IsActive);

        // An account only counts as having a role if its AccountRoles row is active.
        if (roleId.HasValue)
            query = query.Where(a => a.AccountRoles.Any(ar => ar.RoleId == roleId.Value && ar.IsActive));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.FullName.Contains(s) || a.WorkerId.Contains(s) || a.Email.Contains(s));
        }

        var accounts = await query.OrderBy(a => a.FullName).ToListAsync();
        return accounts.Select(AccountDto.FromEntity).ToList();
    }

    public async Task<ServiceResult<AccountDto>> GetByIdAsync(int id)
    {
        var account = await AccountsWithLinks().AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == id);

        return account == null
            ? ServiceResult<AccountDto>.NotFound($"Account {id} was not found.")
            : ServiceResult<AccountDto>.Ok(AccountDto.FromEntity(account));
    }

    // ---------- Create ----------

    public async Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequest request, int adminAccountId)
    {
        var workerId = request.WorkerId.Trim().ToUpper();
        var email = request.Email.Trim().ToLower();

        var check = await CheckRolesAndLocationsAsync(request.RoleIds, request.LocationIds, request.DefaultLocationId);
        if (check != null)
            return ServiceResult<AccountDto>.BadRequest(check);

        if (await _db.Accounts.AnyAsync(a => a.WorkerId == workerId))
            return ServiceResult<AccountDto>.Conflict($"Worker ID '{workerId}' already has an account.");

        if (await _db.Accounts.AnyAsync(a => a.Email == email))
            return ServiceResult<AccountDto>.Conflict($"Email '{email}' is already in use.");

        var now = DateTime.UtcNow;
        var account = new Account
        {
            WorkerId = workerId,
            FullName = request.FullName.Trim(),
            Email = email,
            IsActive = true,
            CreatedAt = now
        };
        account.PasswordHash = _hasher.HashPassword(account, request.Password);

        // Add the role and location links to the account before saving it...
        foreach (var roleId in request.RoleIds.Distinct())
            account.AccountRoles.Add(new AccountRole { RoleId = roleId, AssignedAt = now, AssignedBy = adminAccountId, IsActive = true });

        var defaultLocationId = request.DefaultLocationId ?? request.LocationIds.FirstOrDefault();
        foreach (var locationId in request.LocationIds.Distinct())
            account.AccountLocations.Add(new AccountLocation
            {
                LocationId = locationId,
                IsDefault = locationId == defaultLocationId,
                AssignedAt = now,
                AssignedBy = adminAccountId,
                IsActive = true
            });

        // ...so a single SaveChanges writes the account and all of its links in one transaction.
        // Either everything is saved, or nothing is.
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(account.AccountId);
    }

    // ---------- Update ----------

    public async Task<ServiceResult<AccountDto>> UpdateAsync(int id, UpdateAccountRequest request, int adminAccountId)
    {
        var account = await AccountsWithLinks().FirstOrDefaultAsync(a => a.AccountId == id);
        if (account == null)
            return ServiceResult<AccountDto>.NotFound($"Account {id} was not found.");

        var check = await CheckRolesAndLocationsAsync(request.RoleIds, request.LocationIds, request.DefaultLocationId);
        if (check != null)
            return ServiceResult<AccountDto>.BadRequest(check);

        // An administrator can't lock themselves out.
        if (id == adminAccountId)
        {
            if (!request.IsActive)
                return ServiceResult<AccountDto>.BadRequest("You can't deactivate your own account.");

            var adminRoleId = await _db.Roles.Where(r => r.RoleName == RoleNames.Administrator)
                                             .Select(r => r.RoleId).FirstOrDefaultAsync();
            if (!request.RoleIds.Contains(adminRoleId))
                return ServiceResult<AccountDto>.BadRequest("You can't remove your own Administrator role.");
        }

        var email = request.Email.Trim().ToLower();
        if (await _db.Accounts.AnyAsync(a => a.Email == email && a.AccountId != id))
            return ServiceResult<AccountDto>.Conflict($"Email '{email}' is already in use.");

        var now = DateTime.UtcNow;
        account.FullName = request.FullName.Trim();
        account.Email = email;
        account.IsActive = request.IsActive;
        account.UpdatedAt = now;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
            account.PasswordHash = _hasher.HashPassword(account, request.NewPassword);

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // The database allows only one active default location per account and checks this after
            // every row change. So the current default is cleared and saved first, and only then is
            // the new one set. Otherwise there would briefly be two defaults and the save would fail.
            foreach (var link in account.AccountLocations.Where(l => l.IsDefault))
                link.IsDefault = false;
            await _db.SaveChangesAsync();

            SyncRoles(account, request.RoleIds, adminAccountId, now);
            SyncLocations(account, request.LocationIds, request.DefaultLocationId, adminAccountId, now);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync(); // all of the changes are kept, or none of them
        }
        catch (DbUpdateConcurrencyException)
        {
            // The RowVersion changed between loading and saving, so someone else edited it first.
            return ServiceResult<AccountDto>.Conflict("This account was changed by someone else. Reload and try again.");
        }

        return await GetByIdAsync(id);
    }

    // Makes the account's active roles match roleIds exactly.
    // SQL only allows one row per account and role, so rows are switched on or off instead of
    // being deleted and added again. Inactive rows are kept as history.
    private static void SyncRoles(Account account, List<int> roleIds, int adminAccountId, DateTime now)
    {
        var wanted = roleIds.Distinct().ToList();

        foreach (var link in account.AccountRoles)
        {
            var shouldBeActive = wanted.Contains(link.RoleId);
            if (shouldBeActive && !link.IsActive)
            {
                link.IsActive = true;
                link.AssignedAt = now;
                link.AssignedBy = adminAccountId;
            }
            else if (!shouldBeActive && link.IsActive)
            {
                link.IsActive = false;
            }
        }

        foreach (var roleId in wanted.Where(r => account.AccountRoles.All(link => link.RoleId != r)))
            account.AccountRoles.Add(new AccountRole { RoleId = roleId, AssignedAt = now, AssignedBy = adminAccountId, IsActive = true });
    }

    // Same as SyncRoles, but for locations, making sure exactly one of them is the default.
    private static void SyncLocations(Account account, List<int> locationIds, int? defaultLocationId,
                                      int adminAccountId, DateTime now)
    {
        var wanted = locationIds.Distinct().ToList();
        var defaultId = defaultLocationId ?? wanted.FirstOrDefault();

        foreach (var link in account.AccountLocations)
        {
            var shouldBeActive = wanted.Contains(link.LocationId);
            if (shouldBeActive && !link.IsActive)
            {
                link.IsActive = true;
                link.AssignedAt = now;
                link.AssignedBy = adminAccountId;
            }
            else if (!shouldBeActive && link.IsActive)
            {
                link.IsActive = false;
            }
            link.IsDefault = shouldBeActive && link.LocationId == defaultId;
        }

        foreach (var locationId in wanted.Where(l => account.AccountLocations.All(link => link.LocationId != l)))
            account.AccountLocations.Add(new AccountLocation
            {
                LocationId = locationId,
                IsDefault = locationId == defaultId,
                AssignedAt = now,
                AssignedBy = adminAccountId,
                IsActive = true
            });
    }

    // Checks shared by create and update. Returns an error message, or null if everything is valid.
    private async Task<string?> CheckRolesAndLocationsAsync(List<int> roleIds, List<int> locationIds, int? defaultLocationId)
    {
        var roleNames = await _db.Roles.Where(r => roleIds.Contains(r.RoleId)).Select(r => r.RoleName).ToListAsync();
        if (roleNames.Count != roleIds.Distinct().Count())
            return "One or more role IDs do not exist.";

        var validLocations = await _db.Locations.CountAsync(l => locationIds.Contains(l.LocationId) && l.IsActive);
        if (validLocations != locationIds.Distinct().Count())
            return "One or more location IDs do not exist or are deactivated.";

        // Workers and Supervisors only see their assigned locations, so they need at least one.
        var needsLocation = roleNames.Contains(RoleNames.Worker) || roleNames.Contains(RoleNames.Supervisor);
        if (needsLocation && locationIds.Count == 0)
            return "Workers and Supervisors must be assigned at least one location.";

        if (defaultLocationId.HasValue && !locationIds.Contains(defaultLocationId.Value))
            return "The default location must be one of the assigned locations.";

        return null;
    }

    // ---------- Delete (soft) ----------

    // Accounts are linked to old reports, so they are deactivated instead of deleted.
    // RoleRefresher then rejects this person's token on their next request.
    public async Task<ServiceResult<bool>> DeleteAsync(int id, int adminAccountId)
    {
        if (id == adminAccountId)
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
