using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;

namespace MineSafety.Api.Controllers;

// A parent class every controller inherits from, so the shared bits are written once.
[ApiController]   // switches on API behaviour: automatic 400 for invalid input, JSON in and out
public abstract class ApiControllerBase : ControllerBase
{
    // The logged-in user's account ID, read from the "sub" claim inside their token.
    protected int CurrentAccountId => int.Parse(User.FindFirst("sub")!.Value);

    // Turns a failed ServiceResult into the right HTTP status code with a { "message": "..." } body.
    protected IActionResult ErrorResponse<T>(ServiceResult<T> result) => result.Error switch
    {
        ServiceError.NotFound => NotFound(new { message = result.Message }),
        ServiceError.Conflict => Conflict(new { message = result.Message }),
        _ => BadRequest(new { message = result.Message })
    };
}
