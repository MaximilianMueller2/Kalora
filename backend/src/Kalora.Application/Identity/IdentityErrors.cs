using Kalora.Application.Common;
using Microsoft.AspNetCore.Identity;

namespace Kalora.Application.Identity;

internal static class IdentityErrors
{
    public static Error ToError(this IdentityResult result)
    {
        var code = result.Errors.FirstOrDefault()?.Code ?? string.Empty;
        return code switch
        {
            nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName) =>
                Error.Conflict("auth.email_taken", "email"),
            nameof(IdentityErrorDescriber.PasswordTooShort) => Error.Validation("auth.password_too_short", "password"),
            nameof(IdentityErrorDescriber.InvalidToken) => Error.Validation("auth.invalid_token"),
            nameof(IdentityErrorDescriber.InvalidEmail) => Error.Validation("validation.email_invalid", "email"),
            _ when code.StartsWith("Password", StringComparison.Ordinal) => Error.Validation("auth.password_invalid", "password"),
            _ => Error.Validation("auth.identity_error"),
        };
    }
}
