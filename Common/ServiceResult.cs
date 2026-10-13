namespace MineSafety.Api.Common;

// What a service hands back to a controller: either it worked (with a value),
// or it failed with a reason. The controller turns the reason into a status code:
//   NotFound -> 404,  Conflict -> 409,  BadRequest -> 400
public enum ServiceError
{
    None,
    NotFound,
    Conflict,
    BadRequest
}

public class ServiceResult<T>
{
    public bool Success { get; private init; }
    public T? Value { get; private init; }
    public ServiceError Error { get; private init; }
    public string? Message { get; private init; }

    public static ServiceResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static ServiceResult<T> NotFound(string message) => new() { Error = ServiceError.NotFound, Message = message };
    public static ServiceResult<T> Conflict(string message) => new() { Error = ServiceError.Conflict, Message = message };
    public static ServiceResult<T> BadRequest(string message) => new() { Error = ServiceError.BadRequest, Message = message };
}
