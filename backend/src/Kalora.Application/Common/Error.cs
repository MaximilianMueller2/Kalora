namespace Kalora.Application.Common;

/// <summary>
/// A failure with a stable, translatable <see cref="Code"/> (e.g. "booking.slot_unavailable").
/// <see cref="Parameter"/> names the offending input when the code alone is not enough.
/// </summary>
public sealed record Error(ErrorType Type, string Code, string? Parameter = null)
{
    public static Error Validation(string code, string? parameter = null) => new(ErrorType.Validation, code, parameter);

    public static Error Unauthorized(string code = "auth.unauthorized") => new(ErrorType.Unauthorized, code);

    public static Error Forbidden(string code = "auth.forbidden") => new(ErrorType.Forbidden, code);

    public static Error NotFound(string code, string? parameter = null) => new(ErrorType.NotFound, code, parameter);

    public static Error Conflict(string code, string? parameter = null) => new(ErrorType.Conflict, code, parameter);

    public static Error Unprocessable(string code, string? parameter = null) => new(ErrorType.Unprocessable, code, parameter);
}
