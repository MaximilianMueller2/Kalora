using Kalora.Application.Common;
using Kalora.Domain.Scheduling;

namespace Kalora.Application.Availability;

/// <summary>Loads what is needed from the database and lets the pure scheduling logic work out the free start times.</summary>
public interface IAvailabilityService
{
    /// <summary>
    /// Days with at least one free start time for a service, with the people who are free then (internal data).
    /// Errors: <c>availability.range_invalid</c>, <c>service.not_found</c>, <c>staff.not_found</c> (person unknown or does not offer the service).
    /// </summary>
    Task<Result<IReadOnlyList<DaySlots>>> GetSlotsAsync(AvailabilityQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks one start time again, for the moment of booking. Returns the person to take: the requested one,
    /// or the best one for "anyone". <c>booking.slot_unavailable</c> if the start is not (any longer) offered.
    /// </summary>
    Task<Result<FreeStaff>> CheckSlotAsync(SlotCheckQuery query, CancellationToken cancellationToken = default);
}
