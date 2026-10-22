namespace MineSafety.Api.Common;

// What a service hands back to a controller: either it worked (with a value),
// or it failed with a reason. The controller turns the reason into a status code:
//   NotFound -> 404,  Conflict -> 409,  BadRequest -> 400,  TooLarge -> 413
// "Code" is a short fixed word the app can check in code (e.g. "ALREADY_REVIEWED"),
// "Message" is the human sentence to show on screen.
public enum ServiceError
{
    None,
    NotFound,
    Conflict,
    BadRequest,
    TooLarge
}

public class ServiceResult<T>
{
    public bool Success { get; private init; }
    public T? Value { get; private init; }
    public ServiceError Error { get; private init; }
    public string? Code { get; private init; }
    public string? Message { get; private init; }

    public static ServiceResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static ServiceResult<T> NotFound(string message, string code = "NOT_FOUND") =>
        new() { Error = ServiceError.NotFound, Message = message, Code = code };
    public static ServiceResult<T> Conflict(string message, string code = "CONFLICT") =>
        new() { Error = ServiceError.Conflict, Message = message, Code = code };
    public static ServiceResult<T> BadRequest(string message, string code = "BAD_REQUEST") =>
        new() { Error = ServiceError.BadRequest, Message = message, Code = code };
    public static ServiceResult<T> TooLarge(string message, string code = "FILE_TOO_LARGE") =>
        new() { Error = ServiceError.TooLarge, Message = message, Code = code };
}
