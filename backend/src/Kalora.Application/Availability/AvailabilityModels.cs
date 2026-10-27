namespace Kalora.Application.Availability;

/// <summary>Who asks. Customers only get what the salon offers online and must respect lead time and horizon.</summary>
public enum AvailabilityAudience
{
    /// <summary>The public booking page: active services bookable online, people bookable online, lead time and horizon apply.</summary>
    Customer,

    /// <summary>The salon's own staff (manual appointments): every active service and person, no lead time or horizon.</summary>
    Internal,
}

/// <param name="StaffId">Only this person; null for "anyone".</param>
/// <param name="FromDate">First local day (salon time zone), inclusive.</param>
/// <param name="ToDate">Last local day, inclusive; at most <see cref="AvailabilityService.MaxDays"/> days after the first.</param>
public sealed record AvailabilityQuery(Guid ServiceId, Guid? StaffId, DateOnly FromDate, DateOnly ToDate, AvailabilityAudience Audience);

/// <param name="StartUtc">The exact start of the service, as returned by <see cref="IAvailabilityService.GetSlotsAsync"/>.</param>
public sealed record SlotCheckQuery(Guid ServiceId, Guid? StaffId, DateTimeOffset StartUtc, AvailabilityAudience Audience);
