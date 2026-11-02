namespace Kalora.Application.Onboarding;

/// <param name="Key">One of <see cref="OnboardingSteps"/>.</param>
public sealed record OnboardingStepDto(string Key, bool Done);

/// <param name="Complete">Everything a customer needs to book is in place.</param>
/// <param name="BookingUrl">The salon's public booking page, for the last step of the assistant.</param>
public sealed record OnboardingStatusDto(IReadOnlyList<OnboardingStepDto> Steps, bool Complete, string BookingUrl);

/// <summary>The required steps of the setup assistant, in the order the assistant shows them.</summary>
public static class OnboardingSteps
{
    public const string Profile = "profile";
    public const string Hours = "hours";
    public const string Services = "services";
    public const string Team = "team";
}
