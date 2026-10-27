using Kalora.Application.Availability;
using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.Extensions.Time.Testing;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class AvailabilityServiceTests(PostgresFixture postgres)
{
    // Monday 2 November 2026, 06:00 UTC = 07:00 in Berlin. The salon defaults: 15-minute grid, 2 h lead time, 60 days.
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    private sealed record Setup(string ConnectionString, Guid SalonId, FakeTimeProvider Clock)
    {
        public async Task<T> WithDbAsync<T>(Func<Kalora.Infrastructure.Persistence.AppDbContext, Task<T>> action)
        {
            await using var db = TestDb.CreateContext(ConnectionString, salonId: SalonId);
            return await action(db);
        }

        public async Task<Result<IReadOnlyList<Kalora.Domain.Scheduling.DaySlots>>> SlotsAsync(AvailabilityQuery query) =>
            await WithDbAsync(db => new AvailabilityService(db, Clock).GetSlotsAsync(query));

        public async Task<Result<Kalora.Domain.Scheduling.FreeStaff>> CheckAsync(SlotCheckQuery query) =>
            await WithDbAsync(db => new AvailabilityService(db, Clock).CheckSlotAsync(query));
    }

    private async Task<(AuthTestHost Host, Setup Salon)> NewSalonAsync(string email = "maria@example.com", string name = "Haarwerk")
    {
        var host = await AuthTestHost.StartAsync(postgres);
        var (_, me) = await host.RegisterOwnerAsync(email, name);
        return (host, new Setup(host.ConnectionString, me.Salon!.Id, new FakeTimeProvider(Now)));
    }

    private static async Task<Service> AddServiceAsync(Setup salon, int minutes = 60, int before = 0, int after = 0, bool active = true, bool online = true)
    {
        return await salon.WithDbAsync(async db =>
        {
            var category = new ServiceCategory { Name = "Damen" };
            db.ServiceCategories.Add(category);
            await db.SaveChangesAsync();
            var service = new Service
            {
                CategoryId = category.Id,
                Name = "Schneiden",
                DurationMinutes = minutes,
                BufferBeforeMinutes = before,
                BufferAfterMinutes = after,
                IsActive = active,
                IsBookableOnline = online,
            };
            db.Services.Add(service);
            await db.SaveChangesAsync();
            return service;
        });
    }

    /// <summary>A staff member who offers the service and works Monday and Tuesday 09:00-12:00 (the salon opens 08:00-18:00).</summary>
    private static async Task<StaffMember> AddStaffAsync(Setup salon, Service service, string name = "Anna", int sortOrder = 0, bool active = true, bool online = true,
        int? durationOverride = null, bool offersService = true, bool works = true)
    {
        return await salon.WithDbAsync(async db =>
        {
            var staff = new StaffMember { DisplayName = name, SortOrder = sortOrder, IsActive = active, IsBookableOnline = online };
            db.StaffMembers.Add(staff);
            await db.SaveChangesAsync();
            if (offersService)
            {
                db.StaffServices.Add(new StaffService { StaffId = staff.Id, ServiceId = service.Id, DurationOverrideMinutes = durationOverride });
            }

            if (works)
            {
                foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday })
                {
                    db.StaffWorkingHours.Add(new StaffWorkingHours { StaffId = staff.Id, Weekday = day, Start = new TimeOnly(9, 0), End = new TimeOnly(12, 0), ValidFrom = new DateOnly(2026, 1, 1) });
                }
            }

            await db.SaveChangesAsync();
            return staff;
        });
    }

    private static async Task OpenSalonAsync(Setup salon)
    {
        await salon.WithDbAsync(async db =>
        {
            foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday })
            {
                db.SalonOpeningHours.Add(new SalonOpeningHours { Weekday = day, Start = new TimeOnly(8, 0), End = new TimeOnly(18, 0) });
            }

            await db.SaveChangesAsync();
            return 0;
        });
    }

    private static async Task<Guid> BookAsync(Setup salon, Service service, StaffMember staff, DateTimeOffset start, AppointmentStatus status, int bufferBefore = 0, int bufferAfter = 0, int minutes = 60)
    {
        return await salon.WithDbAsync(async db =>
        {
            var customer = new Customer { Name = "Kim", Email = $"k-{Guid.NewGuid():N}@example.com" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var appointment = new Appointment
            {
                StaffId = staff.Id,
                ServiceId = service.Id,
                CustomerId = customer.Id,
                StartUtc = start,
                EndUtc = start.AddMinutes(minutes),
                BlockedStartUtc = start.AddMinutes(-bufferBefore),
                BlockedEndUtc = start.AddMinutes(minutes + bufferAfter),
                Status = status,
                ServiceName = "Schneiden",
                DurationMinutes = minutes,
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            return appointment.Id;
        });
    }

    private static AvailabilityQuery Query(Service service, DateOnly? from = null, DateOnly? to = null, Guid? staffId = null, AvailabilityAudience audience = AvailabilityAudience.Customer) =>
        new(service.Id, staffId, from ?? Monday, to ?? from ?? Monday, audience);

    private static List<string> LocalStarts(Result<IReadOnlyList<Kalora.Domain.Scheduling.DaySlots>> result, DateOnly date)
    {
        Assert.True(result.IsSuccess, result.IsSuccess ? "" : result.Error.Code);
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        return (result.Value.FirstOrDefault(d => d.Date == date)?.Slots ?? [])
            .Select(s => TimeZoneInfo.ConvertTime(s.StartUtc, berlin).ToString("HH:mm"))
            .ToList();
    }

    private static DateTimeOffset Berlin(DateOnly date, string time) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.Parse(time)), TimeSpan.FromHours(1)).ToUniversalTime(); // winter time in November

    [Fact]
    public async Task Offers_the_grid_times_of_the_working_hours_after_the_lead_time()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        await AddStaffAsync(salon, service);

        var starts = LocalStarts(await salon.SlotsAsync(Query(service)), Monday);
        var tuesday = LocalStarts(await salon.SlotsAsync(Query(service, Tuesday)), Tuesday);

        // Now is 07:00 and the lead time is two hours, but the person only starts at 09:00.
        Assert.Equal(["09:00", "09:15", "09:30", "09:45", "10:00", "10:15", "10:30", "10:45", "11:00"], starts);
        Assert.Equal(9, tuesday.Count);
    }

    [Fact]
    public async Task Open_and_confirmed_appointments_block_but_declined_and_cancelled_ones_do_not()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        var anna = await AddStaffAsync(salon, service);
        // 09:00 confirmed (blocks 09:00-10:00), 10:00 requested (10:00-11:00), 11:00 declined, 11:15 cancelled.
        await BookAsync(salon, service, anna, Berlin(Monday, "09:00"), AppointmentStatus.Confirmed);
        await BookAsync(salon, service, anna, Berlin(Monday, "10:00"), AppointmentStatus.Requested);
        await BookAsync(salon, service, anna, Berlin(Monday, "11:00"), AppointmentStatus.Declined);

        var starts = LocalStarts(await salon.SlotsAsync(Query(service)), Monday);

        Assert.Equal(["11:00"], starts);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Expired)]
    [InlineData(AppointmentStatus.Declined)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Appointments_that_are_over_do_not_block_the_calendar(AppointmentStatus status)
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        var anna = await AddStaffAsync(salon, service);
        await BookAsync(salon, service, anna, Berlin(Monday, "09:00"), status);

        Assert.Contains("09:00", LocalStarts(await salon.SlotsAsync(Query(service)), Monday));
    }

    [Fact]
    public async Task The_buffers_of_existing_appointments_block_too()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon, minutes: 30);
        var anna = await AddStaffAsync(salon, service);
        // 10:00-10:30 with 15 minutes buffer on each side occupies 09:45-10:45.
        await BookAsync(salon, service, anna, Berlin(Monday, "10:00"), AppointmentStatus.Confirmed, bufferBefore: 15, bufferAfter: 15, minutes: 30);

        var starts = LocalStarts(await salon.SlotsAsync(Query(service)), Monday);

        Assert.Contains("09:15", starts);
        Assert.DoesNotContain("09:30", starts);
        Assert.DoesNotContain("10:30", starts);
        Assert.Contains("10:45", starts);
    }

    [Fact]
    public async Task Blockers_and_absences_take_the_person_out_of_that_time()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon, minutes: 30);
        var anna = await AddStaffAsync(salon, service);
        await salon.WithDbAsync(async db =>
        {
            db.TimeBlocks.Add(new TimeBlock { StaffId = anna.Id, StartUtc = Berlin(Monday, "09:00"), EndUtc = Berlin(Monday, "10:00"), Label = "Pause" });
            db.StaffAbsences.Add(new StaffAbsence { StaffId = anna.Id, StartUtc = Berlin(Monday, "11:00"), EndUtc = Berlin(Tuesday, "00:00"), Type = AbsenceType.Sick });
            await db.SaveChangesAsync();
            return 0;
        });

        var starts = LocalStarts(await salon.SlotsAsync(Query(service)), Monday);

        Assert.Equal(["10:00", "10:15", "10:30"], starts);
    }

    [Fact]
    public async Task A_closure_removes_the_day_for_everyone()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        await AddStaffAsync(salon, service);
        await salon.WithDbAsync(async db =>
        {
            db.SalonClosures.Add(new SalonClosure { Date = Monday, Reason = "Betriebsferien" });
            await db.SaveChangesAsync();
            return 0;
        });

        var result = await salon.SlotsAsync(Query(service, Monday, Tuesday));

        Assert.Equal([Tuesday], result.Value.Select(d => d.Date));
    }

    [Fact]
    public async Task Working_hours_follow_their_validity_dates()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        var anna = await AddStaffAsync(salon, service, works: false);
        await salon.WithDbAsync(async db =>
        {
            db.StaffWorkingHours.AddRange(
                new StaffWorkingHours { StaffId = anna.Id, Weekday = DayOfWeek.Monday, Start = new TimeOnly(9, 0), End = new TimeOnly(10, 0), ValidFrom = new DateOnly(2026, 10, 1), ValidTo = new DateOnly(2026, 11, 8) },
                new StaffWorkingHours { StaffId = anna.Id, Weekday = DayOfWeek.Monday, Start = new TimeOnly(14, 0), End = new TimeOnly(15, 0), ValidFrom = new DateOnly(2026, 11, 9) });
            await db.SaveChangesAsync();
            return 0;
        });
        var nextMonday = Monday.AddDays(7);

        var result = await salon.SlotsAsync(Query(service, Monday, nextMonday));

        Assert.Equal(["09:00"], LocalStarts(result, Monday));
        Assert.Equal(["14:00"], LocalStarts(result, nextMonday));
    }

    [Fact]
    public async Task Only_active_people_who_offer_the_service_and_are_bookable_online_are_offered_to_customers()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        var anna = await AddStaffAsync(salon, service, "Anna");
        var inactive = await AddStaffAsync(salon, service, "Inaktiv", active: false);
        var internalOnly = await AddStaffAsync(salon, service, "Intern", online: false);
        var noSkill = await AddStaffAsync(salon, service, "Ohne", offersService: false);

        var forCustomers = (await salon.SlotsAsync(Query(service))).Value.SelectMany(d => d.Slots).SelectMany(s => s.Staff).Select(s => s.StaffId).Distinct();
        var internalView = (await salon.SlotsAsync(Query(service, audience: AvailabilityAudience.Internal))).Value.SelectMany(d => d.Slots).SelectMany(s => s.Staff).Select(s => s.StaffId).Distinct();

        Assert.Equal([anna.Id], forCustomers);
        Assert.Equivalent(new[] { anna.Id, internalOnly.Id }, internalView);
        _ = (inactive, noSkill);
    }

    [Fact]
    public async Task A_service_that_is_inactive_or_not_bookable_online_is_unknown_to_customers()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var hidden = await AddServiceAsync(salon, online: false);
        var inactive = await AddServiceAsync(salon, active: false);
        await AddStaffAsync(salon, hidden);

        var forCustomers = await salon.SlotsAsync(Query(hidden));
        var inactiveResult = await salon.SlotsAsync(Query(inactive, audience: AvailabilityAudience.Internal));
        var internalView = await salon.SlotsAsync(Query(hidden, audience: AvailabilityAudience.Internal));

        Assert.Equal("service.not_found", forCustomers.Error.Code);
        Assert.Equal("service.not_found", inactiveResult.Error.Code);
        Assert.True(internalView.IsSuccess);
        Assert.NotEmpty(internalView.Value);
    }

    [Fact]
    public async Task A_specific_person_is_checked_and_an_unsuitable_one_is_not_found()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        var anna = await AddStaffAsync(salon, service, "Anna");
        var ben = await AddStaffAsync(salon, service, "Ben", sortOrder: 1);
        var noSkill = await AddStaffAsync(salon, service, "Ohne", offersService: false);
        await BookAsync(salon, service, ben, Berlin(Monday, "09:00"), AppointmentStatus.Confirmed);

        var forBen = await salon.SlotsAsync(Query(service, staffId: ben.Id));
        var forAnna = await salon.SlotsAsync(Query(service, staffId: anna.Id));
        var unsuitable = await salon.SlotsAsync(Query(service, staffId: noSkill.Id));
        var unknown = await salon.SlotsAsync(Query(service, staffId: Guid.NewGuid()));

        Assert.DoesNotContain("09:00", LocalStarts(forBen, Monday));
        Assert.Contains("09:00", LocalStarts(forAnna, Monday));
        Assert.Equal("staff.not_found", unsuitable.Error.Code);
        Assert.Equal("staff.not_found", unknown.Error.Code);
    }

    [Fact]
    public async Task A_personal_duration_replaces_the_one_of_the_service()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon, minutes: 60);
        var anna = await AddStaffAsync(salon, service, durationOverride: 120);

        var result = await salon.SlotsAsync(Query(service));

        // Working 09:00-12:00 and two hours needed: the last start is 10:00.
        Assert.Equal("10:00", LocalStarts(result, Monday)[^1]);
        Assert.All(result.Value.SelectMany(d => d.Slots).SelectMany(s => s.Staff), s => Assert.Equal(120, s.DurationMinutes));
        _ = anna;
    }

    [Fact]
    public async Task Buffers_of_the_service_must_fit_inside_the_working_time()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon, minutes: 60, before: 15, after: 30);
        await AddStaffAsync(salon, service);

        var starts = LocalStarts(await salon.SlotsAsync(Query(service)), Monday);

        Assert.Equal("09:15", starts[0]);
        Assert.Equal("10:30", starts[^1]);
    }

    [Fact]
    public async Task Customers_respect_the_booking_horizon_but_the_salons_own_staff_do_not()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await salon.WithDbAsync(async db =>
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                db.SalonOpeningHours.Add(new SalonOpeningHours { Weekday = day, Start = new TimeOnly(8, 0), End = new TimeOnly(18, 0) });
            }

            var salonEntity = db.Salons.Single();
            salonEntity.BookingHorizonDays = 3;
            await db.SaveChangesAsync();
            return 0;
        });
        var service = await AddServiceAsync(salon);
        var anna = await AddStaffAsync(salon, service, works: false);
        await salon.WithDbAsync(async db =>
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                db.StaffWorkingHours.Add(new StaffWorkingHours { StaffId = anna.Id, Weekday = day, Start = new TimeOnly(9, 0), End = new TimeOnly(10, 0), ValidFrom = new DateOnly(2026, 1, 1) });
            }

            await db.SaveChangesAsync();
            return 0;
        });

        var customers = await salon.SlotsAsync(Query(service, Monday, Monday.AddDays(10)));
        var internalView = await salon.SlotsAsync(Query(service, Monday, Monday.AddDays(10), audience: AvailabilityAudience.Internal));

        Assert.Equal(4, customers.Value.Count);
        Assert.Equal(11, internalView.Value.Count);
    }

    [Fact]
    public async Task Internal_use_ignores_the_lead_time()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon, minutes: 30);
        var anna = await AddStaffAsync(salon, service, works: false);
        await salon.WithDbAsync(async db =>
        {
            db.StaffWorkingHours.Add(new StaffWorkingHours { StaffId = anna.Id, Weekday = DayOfWeek.Monday, Start = new TimeOnly(7, 0), End = new TimeOnly(10, 0), ValidFrom = new DateOnly(2026, 1, 1) });
            db.SalonOpeningHours.Add(new SalonOpeningHours { Weekday = DayOfWeek.Monday, Start = new TimeOnly(6, 0), End = new TimeOnly(8, 0) });
            await db.SaveChangesAsync();
            return 0;
        });

        var customers = LocalStarts(await salon.SlotsAsync(Query(service)), Monday);
        var internalView = LocalStarts(await salon.SlotsAsync(Query(service, audience: AvailabilityAudience.Internal)), Monday);

        // Now is 07:00 and the lead time two hours: customers start at 09:00. The salon's own staff may book from "now".
        Assert.Equal("09:00", customers[0]);
        Assert.Equal("07:00", internalView[0]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 62)]
    public async Task The_range_must_be_forward_and_at_most_62_days(int fromOffset, int length)
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        var service = await AddServiceAsync(salon);
        var from = Monday.AddDays(5);

        var result = await salon.SlotsAsync(Query(service, from, from.AddDays(fromOffset < 0 ? fromOffset : length)));

        Assert.Equal("availability.range_invalid", result.Error.Code);
    }

    [Fact]
    public async Task A_range_of_exactly_62_days_is_allowed()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        var service = await AddServiceAsync(salon);

        var result = await salon.SlotsAsync(Query(service, Monday, Monday.AddDays(61)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Nothing_of_another_salon_is_used()
    {
        var (host, salon) = await NewSalonAsync("a@example.com", "Salon A");
        await using var hostScope = host;
        var (_, meB) = await host.RegisterOwnerAsync("b@example.com", "Salon B");
        var other = new Setup(host.ConnectionString, meB.Salon!.Id, new FakeTimeProvider(Now));
        await OpenSalonAsync(salon);
        await OpenSalonAsync(other);
        var serviceA = await AddServiceAsync(salon);
        var serviceB = await AddServiceAsync(other);
        await AddStaffAsync(salon, serviceA, "Anna");
        var staffB = await AddStaffAsync(other, serviceB, "Bea");

        var own = (await salon.SlotsAsync(Query(serviceA))).Value.SelectMany(d => d.Slots).SelectMany(s => s.Staff).Select(s => s.StaffId).Distinct().ToList();
        var foreignService = await salon.SlotsAsync(Query(serviceB));
        var foreignStaff = await salon.SlotsAsync(Query(serviceA, staffId: staffB.Id));

        Assert.DoesNotContain(staffB.Id, own);
        Assert.Single(own);
        Assert.Equal("service.not_found", foreignService.Error.Code);
        Assert.Equal("staff.not_found", foreignStaff.Error.Code);
    }

    // ------------------------------------------------------------------ re-checking one start time

    [Fact]
    public async Task A_free_start_time_checks_out_and_names_the_best_person()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon, minutes: 30);
        var anna = await AddStaffAsync(salon, service, "Anna", sortOrder: 0);
        var ben = await AddStaffAsync(salon, service, "Ben", sortOrder: 1);
        // Anna already has an appointment that day, so Ben gets the next one for "anyone".
        await BookAsync(salon, service, anna, Berlin(Monday, "11:00"), AppointmentStatus.Confirmed, minutes: 30);

        var anyone = await salon.CheckAsync(new SlotCheckQuery(service.Id, null, Berlin(Monday, "09:00"), AvailabilityAudience.Customer));
        var specific = await salon.CheckAsync(new SlotCheckQuery(service.Id, anna.Id, Berlin(Monday, "09:00"), AvailabilityAudience.Customer));

        Assert.Equal(ben.Id, anyone.Value.StaffId);
        Assert.Equal(1, (await salon.CheckAsync(new SlotCheckQuery(service.Id, anna.Id, Berlin(Monday, "10:00"), AvailabilityAudience.Customer))).Value.AppointmentsThatDay);
        Assert.Equal(anna.Id, specific.Value.StaffId);
        Assert.Equal(30, specific.Value.DurationMinutes);
    }

    [Fact]
    public async Task Without_an_appointment_the_earlier_column_wins_for_anyone()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        await AddStaffAsync(salon, service, "Ben", sortOrder: 1);
        var anna = await AddStaffAsync(salon, service, "Anna", sortOrder: 0);

        var result = await salon.CheckAsync(new SlotCheckQuery(service.Id, null, Berlin(Monday, "09:00"), AvailabilityAudience.Customer));

        Assert.Equal(anna.Id, result.Value.StaffId);
    }

    [Theory]
    [InlineData("09:00", "booked")] // taken by an appointment
    [InlineData("09:10", "off-grid")] // not on the 15-minute grid
    [InlineData("12:00", "after hours")] // the person has finished
    [InlineData("06:00", "before lead time")]
    public async Task A_start_time_that_is_not_offered_is_reported_as_unavailable(string time, string why)
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        var anna = await AddStaffAsync(salon, service);
        await BookAsync(salon, service, anna, Berlin(Monday, "09:00"), AppointmentStatus.Confirmed);

        var result = await salon.CheckAsync(new SlotCheckQuery(service.Id, null, Berlin(Monday, time), AvailabilityAudience.Customer));

        Assert.False(result.IsSuccess, why);
        Assert.Equal("booking.slot_unavailable", result.Error.Code);
    }

    [Fact]
    public async Task A_taken_slot_is_unavailable_for_the_specific_person_but_not_for_a_colleague()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;
        await OpenSalonAsync(salon);
        var service = await AddServiceAsync(salon);
        var anna = await AddStaffAsync(salon, service, "Anna");
        var ben = await AddStaffAsync(salon, service, "Ben", sortOrder: 1);
        await BookAsync(salon, service, anna, Berlin(Monday, "09:00"), AppointmentStatus.Requested);

        var forAnna = await salon.CheckAsync(new SlotCheckQuery(service.Id, anna.Id, Berlin(Monday, "09:00"), AvailabilityAudience.Customer));
        var forBen = await salon.CheckAsync(new SlotCheckQuery(service.Id, ben.Id, Berlin(Monday, "09:00"), AvailabilityAudience.Customer));
        var anyone = await salon.CheckAsync(new SlotCheckQuery(service.Id, null, Berlin(Monday, "09:00"), AvailabilityAudience.Customer));

        Assert.Equal("booking.slot_unavailable", forAnna.Error.Code);
        Assert.Equal(ben.Id, forBen.Value.StaffId);
        Assert.Equal(ben.Id, anyone.Value.StaffId);
    }

    [Fact]
    public async Task Checking_a_slot_for_an_unknown_service_is_not_found()
    {
        var (host, salon) = await NewSalonAsync();
        await using var hostScope = host;

        var result = await salon.CheckAsync(new SlotCheckQuery(Guid.NewGuid(), null, Berlin(Monday, "09:00"), AvailabilityAudience.Customer));

        Assert.Equal("service.not_found", result.Error.Code);
    }
}
