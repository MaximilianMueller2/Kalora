using Kalora.Application.Appointments;
using Kalora.Application.Auth;
using Kalora.Application.Calendar;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Domain.Appointments;
using Kalora.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Dashboard;

internal sealed class DashboardService(
    IAppDbContext db,
    ICalendarService calendar,
    IAppointmentService appointments,
    AppointmentScope scope,
    IOptions<AppOptions> appOptions,
    TimeProvider timeProvider) : IDashboardService
{
    /// <summary>Shorter free spells are not worth showing: hardly any service fits.</summary>
    public const int MinGapMinutes = 30;

    private const int ShownRequests = 5;

    private static readonly AppointmentStatus[] Counted = [AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow];
    private static readonly AppointmentStatus[] Earning = [AppointmentStatus.Confirmed, AppointmentStatus.Completed];

    public async Task<Result<DashboardDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var salon = await db.Salons.FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found");
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(salon.TimeZone);
        var now = timeProvider.GetUtcNow();
        var today = SalonTime.LocalDate(now, zone);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var sunday = monday.AddDays(6);

        var week = await calendar.GetAsync(new CalendarQuery(monday, sunday, null), cancellationToken);
        if (!week.IsSuccess)
        {
            return week.Error;
        }

        var open = await appointments.ListOpenRequestsAsync(cancellationToken);
        if (!open.IsSuccess)
        {
            return open.Error;
        }

        var dayStart = SalonTime.StartOfDayUtc(today, zone);
        var dayEnd = SalonTime.StartOfDayUtc(today.AddDays(1), zone);
        return new DashboardDto(
            new DashboardTodayDto(today, salon.TimeZone, week.Value.Columns.Select(column => Person(column, dayStart, dayEnd, now)).ToList()),
            new DashboardRequestsDto(open.Value.Count, open.Value.Take(ShownRequests).ToList()),
            Week(week.Value.Columns, monday, sunday),
            scope.SeesAll ? new BookingLinkDto(AppLinks.Booking(appOptions.Value.PublicBaseUrl, salon.Slug)) : null);
    }

    private static DashboardPersonDto Person(CalendarColumnDto column, DateTimeOffset dayStart, DateTimeOffset dayEnd, DateTimeOffset now)
    {
        var day = new Interval(dayStart, dayEnd);
        var working = IntervalSet.Intersect(column.Working.Select(Span), [day]);
        var todays = column.Appointments.Where(a => a.StartUtc >= dayStart && a.StartUtc < dayEnd).ToList();

        // What is left of today: working time from now on, minus everything that takes it.
        var taken = IntervalSet.Union(
            column.Absences.Select(a => new Interval(a.StartUtc, a.EndUtc))
                .Concat(column.Blocks.Select(b => new Interval(b.StartUtc, b.EndUtc)))
                .Concat(column.Appointments
                    .Where(a => a.Status is AppointmentStatus.Requested or AppointmentStatus.Confirmed)
                    .Select(a => new Interval(a.BlockedStartUtc, a.BlockedEndUtc))));
        var rest = now > dayStart ? new Interval(now < dayEnd ? now : dayEnd, dayEnd) : day;
        var gaps = IntervalSet.Subtract(IntervalSet.Intersect(working, [rest]), taken)
            .Where(gap => gap.End - gap.Start >= TimeSpan.FromMinutes(MinGapMinutes))
            .ToList();

        return new DashboardPersonDto(
            column.StaffId,
            column.DisplayName,
            column.Color,
            working.Select(Dto).ToList(),
            gaps.Select(Dto).ToList(),
            todays.Select(a => new DashboardAppointmentDto(a.Id, a.Status, a.StartUtc, a.EndUtc, a.ServiceName, a.Customer.Name)).ToList());
    }

    private static DashboardWeekDto Week(IReadOnlyList<CalendarColumnDto> columns, DateOnly from, DateOnly to)
    {
        var available = 0.0;
        foreach (var column in columns)
        {
            var away = column.Absences.Select(a => new Interval(a.StartUtc, a.EndUtc)).Concat(column.Blocks.Select(b => new Interval(b.StartUtc, b.EndUtc)));
            available += IntervalSet.Subtract(IntervalSet.Union(column.Working.Select(Span)), away).Sum(i => (i.End - i.Start).TotalMinutes);
        }

        var all = columns.SelectMany(c => c.Appointments).ToList();
        var counted = all.Where(a => Counted.Contains(a.Status)).ToList();
        var booked = counted.Sum(a => (a.EndUtc - a.StartUtc).TotalMinutes);
        var earning = all.Where(a => Earning.Contains(a.Status)).ToList();
        var currency = earning.GroupBy(a => a.Currency).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "EUR";
        var revenue = earning.Where(a => a.Currency == currency).ToList();

        return new DashboardWeekDto(
            from,
            to,
            counted.Count,
            (int)Math.Round(booked),
            (int)Math.Round(available),
            available > 0 ? (int)Math.Min(100, Math.Round(100 * booked / available)) : 0,
            all.Count(a => a.Status == AppointmentStatus.NoShow),
            revenue.Sum(a => a.PriceCents),
            currency,
            revenue.Any(a => a.PriceIsFrom));
    }

    private static Interval Span(CalendarSpanDto span) => new(span.StartUtc, span.EndUtc);

    private static CalendarSpanDto Dto(Interval interval) => new(interval.Start, interval.End);
}
