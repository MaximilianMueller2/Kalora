using Kalora.Application.Appointments;
using Kalora.Application.Calendar;
using Kalora.Domain.Appointments;

namespace Kalora.Application.Dashboard;

/// <summary>An appointment of today, as much as a timeline needs.</summary>
public sealed record DashboardAppointmentDto(
    Guid Id,
    AppointmentStatus Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string ServiceName,
    string CustomerName);

/// <param name="Working">Working time of today inside the opening hours.</param>
/// <param name="Gaps">Free time left today, from now on: working time minus appointments (with buffers), blockers and absences, at least <see cref="DashboardService.MinGapMinutes"/> long.</param>
public sealed record DashboardPersonDto(
    Guid StaffId,
    string DisplayName,
    string Color,
    IReadOnlyList<CalendarSpanDto> Working,
    IReadOnlyList<CalendarSpanDto> Gaps,
    IReadOnlyList<DashboardAppointmentDto> Appointments);

public sealed record DashboardTodayDto(DateOnly Date, string TimeZone, IReadOnlyList<DashboardPersonDto> People);

public sealed record DashboardRequestsDto(int Count, IReadOnlyList<AppointmentDto> Items);

/// <summary>
/// The week from Monday to Sunday of the shown days. <paramref name="UtilizationPercent"/> is booked minutes divided by available
/// minutes (working time without absences and blockers), at most 100. The revenue is an estimate from list prices.
/// </summary>
/// <param name="Appointments">Confirmed, attended and missed appointments (open requests and cancelled ones do not count).</param>
/// <param name="NoShows">Appointments of the week marked as missed.</param>
/// <param name="EstimatedRevenueCents">List prices of confirmed and attended appointments; missed ones earn nothing.</param>
/// <param name="IncludesFromPrices">At least one of them has an "from" price, so the real amount may be higher.</param>
public sealed record DashboardWeekDto(
    DateOnly From,
    DateOnly To,
    int Appointments,
    int BookedMinutes,
    int AvailableMinutes,
    int UtilizationPercent,
    int NoShows,
    int EstimatedRevenueCents,
    string Currency,
    bool IncludesFromPrices);

public sealed record BookingLinkDto(string Url);

public sealed record DashboardDto(
    DashboardTodayDto Today,
    DashboardRequestsDto Requests,
    DashboardWeekDto Week,
    BookingLinkDto? BookingLink);

public sealed record BinaryFile(string FileName, string ContentType, byte[] Content);
