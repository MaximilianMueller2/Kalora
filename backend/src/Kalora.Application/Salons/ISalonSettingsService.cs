using Kalora.Application.Common;

namespace Kalora.Application.Salons;

/// <summary>Profile and booking rules of the signed-in user's salon.</summary>
public interface ISalonSettingsService
{
    Task<Result<SalonProfile>> GetProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Managers may edit everything except the slug: changing it breaks printed QR codes and shared links.</summary>
    Task<Result<SalonProfile>> UpdateProfileAsync(SalonProfile profile, CancellationToken cancellationToken = default);

    Task<Result<BookingRules>> GetBookingRulesAsync(CancellationToken cancellationToken = default);

    Task<Result<BookingRules>> UpdateBookingRulesAsync(BookingRules rules, CancellationToken cancellationToken = default);

    Task<Result<SlugSuggestion>> SuggestSlugAsync(string name, CancellationToken cancellationToken = default);
}
