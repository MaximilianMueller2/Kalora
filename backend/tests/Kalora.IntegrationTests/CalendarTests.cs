using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kalora.Application.Appointments;
using Kalora.Application.Calendar;
using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class CalendarTests(PostgresFixture postgres)
{
    // The test host's clock: Tuesday 3 November 2026, 09:00 UTC = 10:00 in Berlin (winter time). The salon is open 08:00-18:00, Anna works 09:00-17:00.
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Wednesday = new(2026, 11, 4);

    private static DateTimeOffset Berlin(string time, DateOnly? date = null) =>
        new DateTimeOffset((date ?? Wednesday).ToDateTime(TimeOnly.Parse(time)), TimeSpan.FromHours(1)).ToUniversalTime();

    private sealed class World : IAsyncDisposable
    {
        public required AuthTestHost Host { get; init; }
        public required ApiSession Owner { get; init; }
        public required SalonSeeder Salon { get; init; }
        public required Service Cut { get; init; }
        public required StaffMember Anna { get; init; }

        public Task<Appointment> AddAsync(string time, AppointmentStatus status = AppointmentStatus.Confirmed, StaffMember? staff = null, DateOnly? date = null, Service? service = null) =>
            Salon.AddAppointmentAsync(service ?? Cut, staff ?? Anna, Berlin(time, date), status, expiresAt: Now.AddDays(1));

        public async ValueTask DisposeAsync() => await Host.DisposeAsync();
    }

    private async Task<World> NewWorldAsync()
    {
        var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com", "Haarwerk Müller");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        await salon.OpenAsync();
        var cut = await salon.AddServiceAsync(minutes: 60);
        var anna = await salon.AddStaffAsync("Anna", 0, offers: cut);
        return new World { Host = host, Owner = owner, Salon = salon, Cut = cut, Anna = anna };
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private static Task<HttpResponseMessage> GetDay(ApiSession session, DateOnly? day = null, string extra = "") =>
        session.GetAsync($"/api/calendar?from={day ?? Wednesday:yyyy-MM-dd}&to={day ?? Wednesday:yyyy-MM-dd}{extra}");

    private static Task<HttpResponseMessage> Create(ApiSession session, World world, string time, object? who = null, Guid? staffId = null, Guid? serviceId = null) =>
        session.PostAsync("/api/calendar/appointments", new
        {
            serviceId = serviceId ?? world.Cut.Id,
            staffId = staffId ?? world.Anna.Id,
            startUtc = Berlin(time),
            customerName = "Walk-in Willi",
            note = "Stammkunde",
        }.WithCustomer(who));

    // ------------------------------------------------------------ reading

    [Fact]
    public async Task A_day_has_one_column_per_active_person_with_working_time_in_the_salons_hours()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Salon.AddStaffAsync("Ben", 1, offers: world.Cut);
        await world.Salon.AddStaffAsync("Inaktiv", 2, active: false, offers: world.Cut);
        await world.Salon.WithDbAsync(async db =>
        {
            // Ben's plan reaches beyond the opening hours; the column shows the part inside them.
            var hours = await db.StaffWorkingHours.Where(h => h.StaffId == ben.Id).ToListAsync();
            hours.ForEach(h => { h.Start = new TimeOnly(7, 0); h.End = new TimeOnly(20, 0); });
            await db.SaveChangesAsync();
            return 0;
        });

        var calendar = await ReadAsync<CalendarDto>(await GetDay(world.Owner));

        Assert.Equal(("Europe/Berlin", 15), (calendar.TimeZone, calendar.SlotMinutes));
        Assert.Equal(["Anna", "Ben"], calendar.Columns.Select(c => c.DisplayName));
        Assert.Equal((Berlin("09:00"), Berlin("17:00")), (calendar.Columns[0].Working.Single().StartUtc, calendar.Columns[0].Working.Single().EndUtc));
        Assert.Equal((Berlin("08:00"), Berlin("18:00")), (calendar.Columns[1].Working.Single().StartUtc, calendar.Columns[1].Working.Single().EndUtc));
        Assert.Equal("#2563EB", calendar.Columns[0].Color);
    }

    [Fact]
    public async Task A_closed_day_has_no_working_time()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.WithDbAsync(async db =>
        {
            db.SalonClosures.Add(new SalonClosure { Date = Wednesday, Reason = "Betriebsferien" });
            await db.SaveChangesAsync();
            return 0;
        });

        var calendar = await ReadAsync<CalendarDto>(await GetDay(world.Owner));

        Assert.Empty(calendar.Columns[0].Working);
    }

    [Fact]
    public async Task Appointments_blocks_and_absences_of_the_day_are_in_the_column()
    {
        await using var world = await NewWorldAsync();
        var requested = await world.AddAsync("09:00", AppointmentStatus.Requested);
        var confirmed = await world.AddAsync("11:00");
        var completed = await world.AddAsync("13:00", AppointmentStatus.Completed);
        var noShow = await world.AddAsync("15:00", AppointmentStatus.NoShow);
        await world.AddAsync("10:00", AppointmentStatus.Cancelled);
        await world.AddAsync("12:00", AppointmentStatus.Declined);
        await world.AddAsync("14:00", AppointmentStatus.Expired);
        await world.AddAsync("09:00", date: Wednesday.AddDays(1)); // another day
        await world.Salon.WithDbAsync(async db =>
        {
            db.TimeBlocks.Add(new TimeBlock { StaffId = world.Anna.Id, StartUtc = Berlin("16:00"), EndUtc = Berlin("16:30"), Label = "Pause" });
            db.TimeBlocks.Add(new TimeBlock { StaffId = world.Anna.Id, StartUtc = Berlin("16:00", Wednesday.AddDays(2)), EndUtc = Berlin("17:00", Wednesday.AddDays(2)) });
            db.StaffAbsences.Add(new StaffAbsence { StaffId = world.Anna.Id, StartUtc = Berlin("00:00"), EndUtc = Berlin("08:00"), Type = AbsenceType.Sick });
            await db.SaveChangesAsync();
            return 0;
        });

        var column = (await ReadAsync<CalendarDto>(await GetDay(world.Owner))).Columns.Single();

        Assert.Equal([requested.Id, confirmed.Id, completed.Id, noShow.Id], column.Appointments.Select(a => a.Id));
        var first = column.Appointments[0];
        Assert.Equal(("Haarschnitt", "Kim Kundin", AppointmentStatus.Requested), (first.ServiceName, first.Customer.Name, first.Status));
        Assert.Equal((Berlin("09:00"), Berlin("10:00")), (first.StartUtc, first.EndUtc));
        var block = Assert.Single(column.Blocks);
        Assert.Equal(("Pause", Berlin("16:00")), (block.Label, block.StartUtc));
        Assert.Equal(AbsenceType.Sick, Assert.Single(column.Absences).Type);
    }

    [Fact]
    public async Task A_week_covers_several_days_in_one_answer()
    {
        await using var world = await NewWorldAsync();
        await world.AddAsync("10:00");
        await world.AddAsync("10:00", date: Wednesday.AddDays(3));

        var response = await world.Owner.GetAsync($"/api/calendar?from=2026-11-02&to=2026-11-08&staffIds={world.Anna.Id}");
        var column = (await ReadAsync<CalendarDto>(response)).Columns.Single();

        Assert.Equal(2, column.Appointments.Count);
        Assert.Equal(7, column.Working.Count); // Anna works every day of the week
    }

    [Fact]
    public async Task The_columns_can_be_narrowed_to_chosen_people()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Salon.AddStaffAsync("Ben", 1, offers: world.Cut);

        var only = await ReadAsync<CalendarDto>(await GetDay(world.Owner, extra: $"&staffIds={ben.Id}"));

        Assert.Equal(["Ben"], only.Columns.Select(c => c.DisplayName));
    }

    [Theory]
    [InlineData("2026-11-05", "2026-11-04")]
    [InlineData("2026-11-01", "2026-11-15")]
    public async Task A_range_that_is_backwards_or_too_long_is_refused(string from, string to)
    {
        await using var world = await NewWorldAsync();

        var response = await world.Owner.GetAsync($"/api/calendar?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("calendar.range_invalid", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Staff_see_only_their_own_column_while_managers_see_everyone()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        var manager = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Mona", "mona@example.com", SalonRole.Manager);
        var benMember = await world.Salon.WithDbAsync(db => db.StaffMembers.AsNoTracking().SingleAsync(s => s.DisplayName == "Ben"));
        await world.AddAsync("10:00");
        await world.AddAsync("11:00", staff: benMember);

        var benView = await ReadAsync<CalendarDto>(await GetDay(ben));
        var peek = await ReadAsync<CalendarDto>(await GetDay(ben, extra: $"&staffIds={world.Anna.Id}"));
        var managerView = await ReadAsync<CalendarDto>(await GetDay(manager));

        Assert.Equal(["Ben"], benView.Columns.Select(c => c.DisplayName));
        Assert.Single(benView.Columns[0].Appointments);
        Assert.Empty(peek.Columns); // asking for a colleague's column gives nothing
        Assert.Equal(3, managerView.Columns.Count);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetDay(world.Host.NewSession())).StatusCode);
    }

    // ------------------------------------------------------------ customers

    [Fact]
    public async Task Customers_are_found_by_a_part_of_name_email_or_phone_and_only_in_this_salon()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.WithDbAsync(async db =>
        {
            db.Customers.AddRange(
                new Kalora.Domain.Customers.Customer { Name = "Anna Schmidt", Email = "anna@example.com", Phone = "+49 170 111" },
                new Kalora.Domain.Customers.Customer { Name = "Bernd Beck", Email = "bernd@example.com", Phone = "+49 171 222" },
                new Kalora.Domain.Customers.Customer { Name = "Anonym Annabel", Email = null, AnonymizedAtUtc = Now });
            await db.SaveChangesAsync();
            return 0;
        });
        var (_, other) = await world.Host.RegisterOwnerAsync("other@example.com", "Anderer Salon");
        await new SalonSeeder(world.Host.ConnectionString, other.Salon!.Id).WithDbAsync(async db =>
        {
            db.Customers.Add(new Kalora.Domain.Customers.Customer { Name = "Anna Fremd", Email = "fremd@example.com" });
            await db.SaveChangesAsync();
            return 0;
        });

        async Task<string[]> Search(string text) =>
            (await ReadAsync<List<CustomerHitDto>>(await world.Owner.GetAsync($"/api/calendar/customers?q={Uri.EscapeDataString(text)}"))).Select(h => h.Name).ToArray();

        Assert.Equal(["Anna Schmidt"], await Search("anna"));
        Assert.Equal(["Bernd Beck"], await Search("BEC"));
        Assert.Equal(["Bernd Beck"], await Search("bernd@exa"));
        Assert.Equal(["Anna Schmidt"], await Search("170 111"));
        Assert.Empty(await Search("a"));
        Assert.Empty(await Search("  "));
        Assert.Empty(await Search("zzz"));
        Assert.Empty(await Search("%%"));
    }

    // ------------------------------------------------------------ manual appointments

    [Fact]
    public async Task A_manual_appointment_is_confirmed_at_once_and_a_customer_with_email_gets_the_confirmation()
    {
        await using var world = await NewWorldAsync();

        var response = await Create(world.Owner, world, "10:00", new { customerEmail = "Willi@Example.com", customerPhone = "+49 170 5551234" });
        var dto = await ReadAsync<AppointmentDto>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((AppointmentStatus.Confirmed, AppointmentSource.Manual, "Walk-in Willi"), (dto.Status, dto.Source, dto.Customer.Name));
        Assert.Equal((Berlin("10:00"), Berlin("11:00")), (dto.StartUtc, dto.EndUtc));
        Assert.Equal("Stammkunde", dto.InternalNote);
        Assert.Null(dto.CustomerNote);
        Assert.Equal([AppointmentStatus.Cancelled, AppointmentStatus.Completed, AppointmentStatus.NoShow], dto.NextStates);
        await world.Salon.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync();
            Assert.Equal(("willi@example.com", "+49 170 5551234", "de"), (customer.Email, customer.Phone, customer.PreferredLanguage));
            var stored = await db.Appointments.AsNoTracking().SingleAsync();
            Assert.Equal((Now, 0), (stored.ConfirmedAtUtc, stored.CalendarSequence));
            Assert.NotNull(stored.ManageTokenHash);
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingConfirmed);
            Assert.Equal("willi@example.com", mail.ToEmail);
            Assert.Equal(stored.Id, JsonDocument.Parse(mail.PayloadJson!).RootElement.GetProperty("AppointmentId").GetGuid());
            Assert.Equal("appointment.created_manually", (await db.AuditLogs.SingleAsync(a => a.Action.StartsWith("appointment."))).Action);
            return 0;
        });
    }

    [Fact]
    public async Task A_customer_without_email_is_booked_without_a_mail()
    {
        await using var world = await NewWorldAsync();

        await ReadAsync<AppointmentDto>(await Create(world.Owner, world, "10:00"));

        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Null((await db.Customers.SingleAsync()).Email);
            Assert.Empty(await db.EmailOutbox.Where(m => m.Type.StartsWith("booking.")).ToListAsync());
            return 0;
        });
    }

    [Fact]
    public async Task An_existing_customer_is_chosen_by_id_or_found_again_by_email()
    {
        await using var world = await NewWorldAsync();
        var known = await world.Salon.WithDbAsync(async db =>
        {
            var customer = new Kalora.Domain.Customers.Customer { Name = "Kim Kundin", Email = "kim@example.com", PreferredLanguage = "tr" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer;
        });

        var byId = await ReadAsync<AppointmentDto>(await Create(world.Owner, world, "10:00", new { customerId = known.Id, customerName = (string?)null }));
        var byEmail = await ReadAsync<AppointmentDto>(await Create(world.Owner, world, "12:00", new { customerEmail = "KIM@example.com" }));

        Assert.Equal((known.Id, known.Id), (byId.CustomerId, byEmail.CustomerId));
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Single(await db.Customers.ToListAsync());
            // The confirmation goes out in the language of the customer.
            Assert.All(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingConfirmed).ToListAsync(), m => Assert.Equal("tr", m.Language));
            return 0;
        });
    }

    [Fact]
    public async Task Something_that_already_began_today_can_still_be_entered_but_is_not_mailed()
    {
        await using var world = await NewWorldAsync();

        var today = new DateOnly(2026, 11, 3); // it is 10:00 in Berlin; the walk-in came at 09:00
        var response = await world.Owner.PostAsync("/api/calendar/appointments", new
        {
            serviceId = world.Cut.Id,
            staffId = world.Anna.Id,
            startUtc = Berlin("09:00", today),
            customerName = "Walk-in Willi",
            customerEmail = "late@example.com",
        });
        var dto = await ReadAsync<AppointmentDto>(response);

        Assert.Equal(Berlin("09:00", today), dto.StartUtc);
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Empty(await db.EmailOutbox.Where(m => m.Type.StartsWith("booking.")).ToListAsync());
            return 0;
        });
    }

    [Theory]
    [InlineData("08:00")] // before Anna starts
    [InlineData("16:30")] // would end after Anna's day
    [InlineData("10:07")] // not on the 15-minute grid
    public async Task The_person_must_be_free_like_for_an_online_booking(string time)
    {
        await using var world = await NewWorldAsync();

        var response = await Create(world.Owner, world, time);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("booking.slot_unavailable", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Taken_time_blockers_and_absences_stop_a_manual_appointment()
    {
        await using var world = await NewWorldAsync();
        await world.AddAsync("10:00");
        await world.Owner.PostAsync("/api/calendar/blocks", new { staffId = world.Anna.Id, startUtc = Berlin("12:00"), endUtc = Berlin("13:00"), label = "Pause" });
        await world.Salon.WithDbAsync(async db =>
        {
            db.StaffAbsences.Add(new StaffAbsence { StaffId = world.Anna.Id, StartUtc = Berlin("14:00"), EndUtc = Berlin("15:00"), Type = AbsenceType.Private });
            await db.SaveChangesAsync();
            return 0;
        });

        foreach (var time in new[] { "10:00", "10:30", "12:00", "14:00", "14:30" })
        {
            Assert.Equal(HttpStatusCode.Conflict, (await Create(world.Owner, world, time)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Created, (await Create(world.Owner, world, "11:00")).StatusCode); // directly after the 10:00 appointment
    }

    [Fact]
    public async Task A_service_the_person_does_not_offer_is_refused()
    {
        await using var world = await NewWorldAsync();
        var color = await world.Salon.AddServiceAsync("Färben", 90);

        var response = await Create(world.Owner, world, "10:00", serviceId: color.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("staff.not_found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_manual_appointment_needs_a_customer_and_a_valid_email()
    {
        await using var world = await NewWorldAsync();

        var nameless = await world.Owner.PostAsync("/api/calendar/appointments", new { serviceId = world.Cut.Id, staffId = world.Anna.Id, startUtc = Berlin("10:00") });
        var badEmail = await Create(world.Owner, world, "10:00", new { customerEmail = "not-an-email" });

        Assert.Equal("validation.required", await nameless.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        Assert.Equal("validation.email_invalid", await badEmail.ProblemCodeAsync());
    }

    [Fact]
    public async Task Staff_book_only_into_their_own_column()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        var benMember = await world.Salon.WithDbAsync(db => db.StaffMembers.AsNoTracking().SingleAsync(s => s.DisplayName == "Ben"));
        await world.Salon.WithDbAsync(async db =>
        {
            db.StaffServices.Add(new StaffService { StaffId = benMember.Id, ServiceId = world.Cut.Id });
            db.StaffWorkingHours.Add(new StaffWorkingHours { StaffId = benMember.Id, Weekday = DayOfWeek.Wednesday, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0), ValidFrom = new DateOnly(2026, 1, 1) });
            await db.SaveChangesAsync();
            return 0;
        });

        var foreign = await Create(ben, world, "10:00", staffId: world.Anna.Id);
        var own = await Create(ben, world, "10:00", staffId: benMember.Id);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
    }

    // ------------------------------------------------------------ moving

    [Fact]
    public async Task Moving_changes_the_time_raises_the_calendar_sequence_and_tells_the_customer()
    {
        await using var world = await NewWorldAsync();
        var appointment = await world.AddAsync("10:00", date: Wednesday.AddDays(1));

        var response = await world.Owner.PostAsync($"/api/calendar/appointments/{appointment.Id}/move", new { startUtc = Berlin("14:00", Wednesday.AddDays(1)), reason = "Krankheit" });
        var dto = await ReadAsync<AppointmentDto>(response);

        Assert.Equal((Berlin("14:00", Wednesday.AddDays(1)), Berlin("15:00", Wednesday.AddDays(1))), (dto.StartUtc, dto.EndUtc));
        await world.Salon.WithDbAsync(async db =>
        {
            var stored = await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id);
            Assert.Equal(1, stored.CalendarSequence);
            Assert.Equal((stored.StartUtc, stored.EndUtc), (stored.BlockedStartUtc, stored.BlockedEndUtc));
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingRescheduled);
            var payload = JsonDocument.Parse(mail.PayloadJson!).RootElement;
            Assert.Equal((appointment.Id, 1, "Krankheit"), (payload.GetProperty("AppointmentId").GetGuid(), payload.GetProperty("CalendarSequence").GetInt32(), payload.GetProperty("Reason").GetString()));
            Assert.Equal(Berlin("14:00", Wednesday.AddDays(1)), payload.GetProperty("StartUtc").GetDateTimeOffset());
            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "appointment.moved");
            Assert.DoesNotContain("Krankheit", audit.Details);
            return 0;
        });
    }

    [Fact]
    public async Task A_small_shift_overlapping_the_old_time_is_fine_because_the_appointment_does_not_block_itself()
    {
        await using var world = await NewWorldAsync();
        var appointment = await world.AddAsync("10:00", date: Wednesday.AddDays(1));

        var response = await world.Owner.PostAsync($"/api/calendar/appointments/{appointment.Id}/move", new { startUtc = Berlin("10:15", Wednesday.AddDays(1)) });

        Assert.Equal(Berlin("10:15", Wednesday.AddDays(1)), (await ReadAsync<AppointmentDto>(response)).StartUtc);
    }

    [Fact]
    public async Task Moving_to_taken_time_is_refused_and_nothing_changes()
    {
        await using var world = await NewWorldAsync();
        var appointment = await world.AddAsync("10:00", date: Wednesday.AddDays(1));
        await world.AddAsync("14:00", date: Wednesday.AddDays(1));

        var response = await world.Owner.PostAsync($"/api/calendar/appointments/{appointment.Id}/move", new { startUtc = Berlin("14:30", Wednesday.AddDays(1)) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("booking.slot_unavailable", await response.ProblemCodeAsync());
        await world.Salon.WithDbAsync(async db =>
        {
            var stored = await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id);
            Assert.Equal((Berlin("10:00", Wednesday.AddDays(1)), 0), (stored.StartUtc, stored.CalendarSequence));
            Assert.Empty(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingRescheduled).ToListAsync());
            return 0;
        });
    }

    [Fact]
    public async Task The_buffers_of_the_service_count_when_moving()
    {
        await using var world = await NewWorldAsync();
        var buffered = await world.Salon.AddServiceAsync("Färben", 60, before: 15, after: 15);
        await world.Salon.WithDbAsync(async db =>
        {
            db.StaffServices.Add(new StaffService { StaffId = world.Anna.Id, ServiceId = buffered.Id });
            await db.SaveChangesAsync();
            return 0;
        });
        var first = await world.AddAsync("10:00", date: Wednesday.AddDays(1), service: buffered); // occupies 09:45-11:15
        var second = await world.AddAsync("14:00", date: Wednesday.AddDays(1), service: buffered);

        var tooClose = await world.Owner.PostAsync($"/api/calendar/appointments/{second.Id}/move", new { startUtc = Berlin("11:15", Wednesday.AddDays(1)) });
        var fits = await world.Owner.PostAsync($"/api/calendar/appointments/{second.Id}/move", new { startUtc = Berlin("11:30", Wednesday.AddDays(1)) });

        Assert.Equal(HttpStatusCode.Conflict, tooClose.StatusCode);
        var moved = await ReadAsync<AppointmentDto>(fits);
        Assert.Equal((Berlin("11:15", Wednesday.AddDays(1)), Berlin("12:45", Wednesday.AddDays(1))), (moved.BlockedStartUtc, moved.BlockedEndUtc));
        Assert.NotEqual(first.Id, moved.Id);
    }

    [Fact]
    public async Task An_appointment_can_move_to_a_colleague_who_offers_the_service()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Salon.AddStaffAsync("Ben", 1, offers: world.Cut);
        var appointment = await world.AddAsync("10:00", date: Wednesday.AddDays(1));

        var dto = await ReadAsync<AppointmentDto>(await world.Owner.PostAsync($"/api/calendar/appointments/{appointment.Id}/move", new { startUtc = Berlin("10:00", Wednesday.AddDays(1)), staffId = ben.Id }));

        Assert.Equal("Ben", dto.Staff.DisplayName);
        await world.Salon.WithDbAsync(async db =>
        {
            var payload = JsonDocument.Parse((await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingRescheduled)).PayloadJson!).RootElement;
            Assert.Equal("Ben", payload.GetProperty("StaffName").GetString());
            return 0;
        });
    }

    [Fact]
    public async Task A_colleague_who_does_not_offer_the_service_cannot_take_the_appointment()
    {
        await using var world = await NewWorldAsync();
        var other = await world.Salon.AddStaffAsync("Olga", 1);
        var appointment = await world.AddAsync("10:00", date: Wednesday.AddDays(1));

        var response = await world.Owner.PostAsync($"/api/calendar/appointments/{appointment.Id}/move", new { startUtc = Berlin("10:00", Wednesday.AddDays(1)), staffId = other.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Moving_to_the_same_place_changes_nothing_and_sends_no_mail()
    {
        await using var world = await NewWorldAsync();
        var appointment = await world.AddAsync("10:00", date: Wednesday.AddDays(1));

        Assert.Equal(HttpStatusCode.OK, (await world.Owner.PostAsync($"/api/calendar/appointments/{appointment.Id}/move", new { startUtc = Berlin("10:00", Wednesday.AddDays(1)) })).StatusCode);

        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Equal(0, (await db.Appointments.AsNoTracking().SingleAsync()).CalendarSequence);
            Assert.Empty(await db.EmailOutbox.Where(m => m.Type.StartsWith("booking.")).ToListAsync());
            return 0;
        });
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Declined)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    public async Task Only_confirmed_appointments_can_be_moved(AppointmentStatus status)
    {
        await using var world = await NewWorldAsync();
        var appointment = await world.AddAsync("10:00", status, date: Wednesday.AddDays(1));

        var response = await world.Owner.PostAsync($"/api/calendar/appointments/{appointment.Id}/move", new { startUtc = Berlin("14:00", Wednesday.AddDays(1)) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("appointment.not_movable", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task An_appointment_that_is_over_cannot_be_moved()
    {
        await using var world = await NewWorldAsync();
        var past = await world.Salon.AddAppointmentAsync(world.Cut, world.Anna, Now.AddHours(-3), AppointmentStatus.Confirmed);

        var response = await world.Owner.PostAsync($"/api/calendar/appointments/{past.Id}/move", new { startUtc = Berlin("14:00") });

        Assert.Equal("appointment.not_movable", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Staff_move_only_their_own_appointments()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        var annas = await world.AddAsync("10:00", date: Wednesday.AddDays(1));

        var response = await ben.PostAsync($"/api/calendar/appointments/{annas.Id}/move", new { startUtc = Berlin("14:00", Wednesday.AddDays(1)) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------ blockers

    [Fact]
    public async Task A_blocker_appears_in_the_column_and_can_be_removed()
    {
        await using var world = await NewWorldAsync();

        var created = await world.Owner.PostAsync("/api/calendar/blocks", new { staffId = world.Anna.Id, startUtc = Berlin("12:00"), endUtc = Berlin("13:00"), label = "  Mittagspause " });
        var block = await ReadAsync<CalendarBlockDto>(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Mittagspause", block.Label);
        Assert.Equal(block.Id, (await ReadAsync<CalendarDto>(await GetDay(world.Owner))).Columns.Single().Blocks.Single().Id);

        var deleted = await world.Owner.DeleteAsync($"/api/calendar/blocks/{block.Id}");

        Assert.True(deleted.IsSuccessStatusCode);
        Assert.Empty((await ReadAsync<CalendarDto>(await GetDay(world.Owner))).Columns.Single().Blocks);
    }

    [Fact]
    public async Task A_blocker_cannot_cover_an_appointment()
    {
        await using var world = await NewWorldAsync();
        await world.AddAsync("10:00");

        var response = await world.Owner.PostAsync("/api/calendar/blocks", new { staffId = world.Anna.Id, startUtc = Berlin("10:30"), endUtc = Berlin("11:30") });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("calendar.block_overlaps_appointment", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_blocker_needs_a_sensible_range()
    {
        await using var world = await NewWorldAsync();

        var backwards = await world.Owner.PostAsync("/api/calendar/blocks", new { staffId = world.Anna.Id, startUtc = Berlin("13:00"), endUtc = Berlin("12:00") });
        var endless = await world.Owner.PostAsync("/api/calendar/blocks", new { staffId = world.Anna.Id, startUtc = Berlin("13:00"), endUtc = Berlin("13:00").AddDays(30) });

        Assert.Equal("calendar.block_range_invalid", await backwards.ProblemCodeAsync());
        Assert.Equal("calendar.block_range_invalid", await endless.ProblemCodeAsync());
    }

    [Fact]
    public async Task Staff_block_only_their_own_time_and_cannot_touch_the_blockers_of_others()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        var annas = await ReadAsync<CalendarBlockDto>(await world.Owner.PostAsync("/api/calendar/blocks", new { staffId = world.Anna.Id, startUtc = Berlin("12:00"), endUtc = Berlin("13:00") }));

        var create = await ben.PostAsync("/api/calendar/blocks", new { staffId = world.Anna.Id, startUtc = Berlin("14:00"), endUtc = Berlin("15:00") });
        var delete = await ben.DeleteAsync($"/api/calendar/blocks/{annas.Id}");

        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Owner.DeleteAsync($"/api/calendar/blocks/{Guid.NewGuid()}")).StatusCode);
    }
}

internal static class CalendarTestExtensions
{
    /// <summary>Merges extra properties (customer id, email, ...) into an anonymous request object.</summary>
    public static Dictionary<string, object?> WithCustomer(this object request, object? extra)
    {
        var json = JsonSerializer.SerializeToElement(request);
        var merged = json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
        if (extra is not null)
        {
            foreach (var property in JsonSerializer.SerializeToElement(extra).EnumerateObject())
            {
                merged[property.Name] = property.Value.Clone();
            }
        }

        return merged;
    }
}
