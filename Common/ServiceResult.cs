namespace MineSafety.Api.Common;

// The kinds of failure a service can report. The controller maps each one to a status code:
// NotFound = 404, Conflict = 409, BadRequest = 400, TooLarge = 413.
public enum ServiceError
{
    None,
    NotFound,
    Conflict,
    BadRequest,
    TooLarge
}

// What a service returns to a controller: either success with a value, or a failure with a reason.
// Code is a short fixed value the app can check (for example "ALREADY_REVIEWED"),
// and Message is the sentence shown to the user.
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
