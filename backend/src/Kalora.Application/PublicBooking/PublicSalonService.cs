using System.Text.RegularExpressions;
using Kalora.Application.Availability;
using Kalora.Application.Common;
using Kalora.Application.Salons;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.PublicBooking;

internal sealed partial class PublicSalonService(IAppDbContext db, IAvailabilityService availability, TimeProvider timeProvider) : IPublicSalonService
{
    private static readonly DayOfWeek[] WeekFromMonday =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    [GeneratedRegex(@"^(\d{4})-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex MonthPattern();

    public async Task<Result<PublicSalonDto>> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var salon = await FindVisibleSalonAsync(cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found", "slug");
        }

        var hours = await db.SalonOpeningHours.ToListAsync(cancellationToken);
        var byDay = hours.ToLookup(h => h.Weekday);
        var days = WeekFromMonday
            .Select(day => new OpeningDay(day, byDay[day].OrderBy(h => h.Start).Select(h => new OpeningBlock(h.Start, h.End)).ToList()))
            .ToList();

        return new PublicSalonDto(
            salon.Name, salon.Slug, salon.Street, salon.PostalCode, salon.City, salon.Phone, salon.TimeZone, salon.DefaultLanguage,
            ValidationExtensions.SupportedLanguages, days, salon.ImprintText, salon.PrivacyPolicyUrl, salon.PrivacyText,
            salon.AccessAt(timeProvider.GetUtcNow()) == SalonAccess.Full);
    }

    public async Task<Result<IReadOnlyList<PublicCategoryDto>>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        var open = await EnsureBookableAsync(cancellationToken);
        if (!open.IsSuccess)
        {
            return open.Error;
        }

        var bookableServiceIds = db.StaffServices
            .Join(db.StaffMembers.Where(s => s.IsActive && s.IsBookableOnline), link => link.StaffId, staff => staff.Id, (link, _) => link.ServiceId);
        var services = await db.Services
            .Where(s => s.IsActive && s.IsBookableOnline && bookableServiceIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var categories = await db.ServiceCategories.OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);

        return categories
            .Select(category => new PublicCategoryDto(
                category.Name,
                services.Where(s => s.CategoryId == category.Id).OrderBy(s => s.SortOrder)
                    .Select(s => new PublicServiceDto(s.Id, s.Name, s.Description, s.PriceCents, s.PriceIsFrom, s.Currency, s.DurationMinutes)).ToList()))
            .Where(category => category.Services.Count > 0)
            .ToList();
    }

    public async Task<Result<IReadOnlyList<PublicStaffDto>>> GetStaffAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var open = await EnsureBookableAsync(cancellationToken);
        if (!open.IsSuccess)
        {
            return open.Error;
        }

        if (!await db.Services.AnyAsync(s => s.Id == serviceId && s.IsActive && s.IsBookableOnline, cancellationToken))
        {
            return Error.NotFound("service.not_found", "serviceId");
        }

        return await db.StaffServices
            .Where(link => link.ServiceId == serviceId)
            .Join(db.StaffMembers.Where(s => s.IsActive && s.IsBookableOnline), link => link.StaffId, staff => staff.Id, (_, staff) => staff)
            .OrderBy(s => s.SortOrder)
            .Select(s => new PublicStaffDto(s.Id, s.DisplayName, s.Color))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<PublicDaysDto>> GetDaysAsync(Guid serviceId, Guid? staffId, string month, CancellationToken cancellationToken = default)
    {
        var match = MonthPattern().Match(month ?? string.Empty);
        if (!match.Success)
        {
            return Error.Validation("availability.month_invalid", "month");
        }

        var first = new DateOnly(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), 1);
        var last = first.AddMonths(1).AddDays(-1);
        var slots = await SlotsAsync(serviceId, staffId, first, last, cancellationToken);
        return slots.IsSuccess ? new PublicDaysDto(month!, slots.Value.Select(d => d.Date).ToList()) : slots.Error;
    }

    public async Task<Result<PublicSlotsDto>> GetSlotsAsync(Guid serviceId, Guid? staffId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var days = await SlotsAsync(serviceId, staffId, date, date, cancellationToken);
        if (!days.IsSuccess)
        {
            return days.Error;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(await db.Salons.Select(s => s.TimeZone).FirstAsync(cancellationToken));
        var slots = days.Value.SelectMany(d => d.Slots)
            .Select(s => new PublicSlotDto(s.StartUtc, TimeZoneInfo.ConvertTime(s.StartUtc, zone).ToString("HH:mm")))
            .ToList();
        return new PublicSlotsDto(date, slots);
    }

    private async Task<Result<IReadOnlyList<Domain.Scheduling.DaySlots>>> SlotsAsync(Guid serviceId, Guid? staffId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var open = await EnsureBookableAsync(cancellationToken);
        if (!open.IsSuccess)
        {
            return open.Error;
        }

        return await availability.GetSlotsAsync(new AvailabilityQuery(serviceId, staffId, from, to, AvailabilityAudience.Customer), cancellationToken);
    }

    /// <summary>
    /// A cancelled salon is gone: its page is "not found". A suspended one or one whose trial is over keeps its page, which says that
    /// online booking is not available at the moment (and shows the phone number); everything else about it is refused.
    /// </summary>
    private Task<Salon?> FindVisibleSalonAsync(CancellationToken cancellationToken) =>
        db.Salons.Where(s => s.Status != SalonStatus.Cancelled).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    private async Task<Result> EnsureBookableAsync(CancellationToken cancellationToken)
    {
        var salon = await FindVisibleSalonAsync(cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found", "slug");
        }

        return salon.AccessAt(timeProvider.GetUtcNow()) == SalonAccess.Full ? Result.Success() : Error.Conflict("booking.unavailable");
    }
}
