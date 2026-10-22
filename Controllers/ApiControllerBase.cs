using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;

namespace MineSafety.Api.Controllers;

// A parent class every controller inherits from, so the shared bits are written once.
[ApiController]   // switches on API behaviour: automatic 400 for invalid input, JSON in and out
public abstract class ApiControllerBase : ControllerBase
{
    // The logged-in user's account ID, read from the "sub" claim inside their token.
    protected int CurrentAccountId => int.Parse(User.FindFirst("sub")!.Value);

    // Turns a failed ServiceResult into the right HTTP status code.
    // Every error has the same shape: { "code": "ALREADY_REVIEWED", "message": "..." }
    protected IActionResult ErrorResponse<T>(ServiceResult<T> result)
    {
        var body = new { code = result.Code, message = result.Message };
        return result.Error switch
        {
            ServiceError.NotFound => NotFound(body),
            ServiceError.Conflict => Conflict(body),
            ServiceError.TooLarge => StatusCode(StatusCodes.Status413PayloadTooLarge, body),
            _ => BadRequest(body)
        };
    }
}
