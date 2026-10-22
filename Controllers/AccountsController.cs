using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Endpoints under /api/accounts. Only administrators can use them.
[Route("api/accounts")]
[Authorize(Roles = RoleNames.Administrator)]
public class AccountsController : ApiControllerBase
{
    private readonly IAccountService _accounts;

    public AccountsController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    // GET /api/accounts?search=bob&roleId=1&includeInactive=true
    // roleId only returns accounts that currently have that role.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search = null, [FromQuery] int? roleId = null,
                                            [FromQuery] bool includeInactive = true)
    {
        return Ok(await _accounts.GetAllAsync(search, roleId, includeInactive));
    }

    // GET /api/accounts/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _accounts.GetByIdAsync(id);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // POST /api/accounts
    // Body: { workerId, fullName, email, password, roleIds: [1], locationIds: [2, 3], defaultLocationId: 2 }
    [HttpPost]
    public async Task<IActionResult> Create(CreateAccountRequest request)
    {
        var result = await _accounts.CreateAsync(request, CurrentAccountId);
        if (!result.Success)
            return ErrorResponse(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.AccountId }, result.Value);
    }

    // PUT /api/accounts/5
    // The body is the complete new state, so any role or location left out of the lists is removed.
    // An administrator can also reset the password here by filling in newPassword.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateAccountRequest request)
    {
        var result = await _accounts.UpdateAsync(id, request, CurrentAccountId);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // DELETE /api/accounts/5
    // Soft delete: the account is deactivated and can no longer log in.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _accounts.DeleteAsync(id, CurrentAccountId);
        return result.Success ? NoContent() : ErrorResponse(result);
    }
}

// GET /api/roles
// Fills the role dropdown on the create and edit account screens.
[Route("api/roles")]
[Authorize(Roles = RoleNames.Administrator)]
public class RolesController : ApiControllerBase
{
    private readonly IAccountService _accounts;

    public RolesController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _accounts.GetRolesAsync());
}
