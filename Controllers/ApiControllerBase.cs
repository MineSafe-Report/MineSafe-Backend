using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;

namespace MineSafety.Api.Controllers;

// Base class for every controller, so the shared helpers are only written once.
// [ApiController] turns on automatic 400 responses for invalid input and JSON in and out.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    // The logged-in user's account ID, read from the "sub" claim in their token.
    protected int CurrentAccountId => int.Parse(User.FindFirst("sub")!.Value);

    // Converts a failed ServiceResult into the matching HTTP status code.
    // Every error uses the same shape: { "code": "ALREADY_REVIEWED", "message": "..." }
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
