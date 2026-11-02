using Kalora.Application.Common;

namespace Kalora.Application.Onboarding;

/// <summary>
/// How far the salon is on its way to being bookable. Nothing is stored: each step counts as done when the data it asks for
/// exists, so the progress is always right, also when the salon changed things outside the assistant.
/// </summary>
public interface IOnboardingService
{
    Task<Result<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default);
}
