using Kalora.Domain.Salons;

namespace Kalora.Application.Auth;

public sealed record RegisterRequest(
    string SalonName, SalonIndustry Industry, string Name, string Email, string Password, string Language, bool AcceptTerms);

public sealed record LoginRequest(
    string Email, string Password, bool RememberMe = false, string? TwoFactorCode = null, string? RecoveryCode = null);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record MeSalon(Guid Id, string Name, string Slug, SalonStatus Status, PlanCode PlanCode, DateTimeOffset? TrialEndsAtUtc);

public sealed record MeResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    string PreferredLanguage,
    bool TwoFactorEnabled,
    bool IsPlatformAdmin,
    MeSalon? Salon,
    SalonRole? Role,
    Guid? StaffId);

public sealed record TwoFactorSetupResponse(string SharedKey, string AuthenticatorUri);

public sealed record TwoFactorCodeRequest(string Code);

public sealed record PasswordConfirmationRequest(string Password);

public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

public sealed record CreateInvitationRequest(Guid StaffId, string Email, SalonRole Role);

public sealed record InvitationCreatedResponse(DateTimeOffset ExpiresAtUtc);

public sealed record InvitationPreviewResponse(
    string SalonName, string StaffDisplayName, string Email, SalonRole Role, DateTimeOffset ExpiresAtUtc);

public sealed record AcceptInvitationRequest(string Password, string Language);
