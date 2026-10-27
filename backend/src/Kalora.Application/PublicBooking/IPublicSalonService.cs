using Kalora.Application.Common;

namespace Kalora.Application.PublicBooking;

/// <summary>Read-only data for the public booking page of the salon named in the URL. Never contains customer data.</summary>
public interface IPublicSalonService
{
    Task<Result<PublicSalonDto>> GetProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Active, online-bookable services that at least one bookable person offers, by category.</summary>
    Task<Result<IReadOnlyList<PublicCategoryDto>>> GetServicesAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PublicStaffDto>>> GetStaffAsync(Guid serviceId, CancellationToken cancellationToken = default);

    /// <param name="month">"yyyy-MM"; error <c>availability.month_invalid</c> otherwise.</param>
    Task<Result<PublicDaysDto>> GetDaysAsync(Guid serviceId, Guid? staffId, string month, CancellationToken cancellationToken = default);

    Task<Result<PublicSlotsDto>> GetSlotsAsync(Guid serviceId, Guid? staffId, DateOnly date, CancellationToken cancellationToken = default);
}
