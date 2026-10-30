using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kalora.Application.Appointments;
using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class AppointmentsTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero); // the fake clock of the test host
    private static readonly DateTimeOffset Tomorrow10 = Now.AddDays(1).AddHours(1);

    private sealed class World : IAsyncDisposable
    {
        public required AuthTestHost Host { get; init; }
        public required ApiSession Owner { get; init; }
        public required SalonSeeder Salon { get; init; }
        public required Service Cut { get; init; }
        public required StaffMember Anna { get; init; }
        public required Guid OwnerUserId { get; init; }

        public Task<Appointment> AddAsync(AppointmentStatus status = AppointmentStatus.Requested, DateTimeOffset? start = null, StaffMember? staff = null, DateTimeOffset? expiresAt = null) =>
            Salon.AddAppointmentAsync(Cut, staff ?? Anna, start ?? Tomorrow10, status, expiresAt ?? Now.AddHours(20));

        public async ValueTask DisposeAsync() => await Host.DisposeAsync();
    }

    private async Task<World> NewWorldAsync()
    {
        var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com", "Haarwerk Müller");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        await salon.OpenAsync();
        await salon.ConfigureSalonAsync(s => { s.Street = "Hauptstraße 1"; s.PostalCode = "10115"; s.City = "Berlin"; s.Phone = "+49 30 123456"; });
        var cut = await salon.AddServiceAsync(minutes: 60);
        var anna = await salon.AddStaffAsync("Anna", 0, offers: cut);
        return new World { Host = host, Owner = owner, Salon = salon, Cut = cut, Anna = anna, OwnerUserId = me.UserId };
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private static string Url(Guid id, string action = "") => $"/api/appointments/{id}{(action.Length > 0 ? "/" + action : "")}";

    // ------------------------------------------------------------ reading

    [Fact]
    public async Task Open_requests_are_listed_with_customer_and_person_the_most_urgent_first()
    {
        await using var world = await NewWorldAsync();
        var later = await world.AddAsync(expiresAt: Now.AddHours(10));
        var sooner = await world.AddAsync(start: Tomorrow10.AddHours(2), expiresAt: Now.AddHours(2));
        await world.AddAsync(AppointmentStatus.Confirmed, Tomorrow10.AddHours(4));
        await world.AddAsync(AppointmentStatus.Declined, Tomorrow10.AddHours(6));

        var list = await ReadAsync<List<AppointmentDto>>(await world.Owner.GetAsync("/api/appointments/requests"));
        var count = await ReadAsync<OpenRequestCount>(await world.Owner.GetAsync("/api/appointments/requests/count"));

        Assert.Equal([sooner.Id, later.Id], list.Select(a => a.Id));
        Assert.Equal(2, count.Count);
        var first = list[0];
        Assert.Equal(("Haarschnitt", "Anna", "Kim Kundin", "+49 170 1234567", "Bitte ohne Föhn"), (first.ServiceName, first.Staff.DisplayName, first.Customer.Name, first.Customer.Phone, first.CustomerNote));
        Assert.Equal(Now.AddHours(2), first.ExpiresAtUtc);
        Assert.Equal([AppointmentStatus.Confirmed, AppointmentStatus.Declined, AppointmentStatus.Expired, AppointmentStatus.Cancelled], first.NextStates);
    }

    [Fact]
    public async Task One_appointment_can_be_read_by_id_in_any_status()
    {
        await using var world = await NewWorldAsync();
        var done = await world.AddAsync(AppointmentStatus.Cancelled);

        var dto = await ReadAsync<AppointmentDto>(await world.Owner.GetAsync(Url(done.Id)));

        Assert.Equal((done.Id, AppointmentStatus.Cancelled), (dto.Id, dto.Status));
        Assert.Empty(dto.NextStates);
    }

    [Fact]
    public async Task An_unknown_appointment_is_not_found_and_anonymous_visitors_are_turned_away()
    {
        await using var world = await NewWorldAsync();

        var missing = await world.Owner.GetAsync(Url(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("appointment.not_found", await missing.ProblemCodeAsync());
        var anonymous = world.Host.NewSession();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/appointments/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/appointments/requests/count")).StatusCode);
    }

    [Fact]
    public async Task Another_salons_appointments_do_not_exist_for_us()
    {
        await using var world = await NewWorldAsync();
        var (otherOwner, otherMe) = await world.Host.RegisterOwnerAsync("other@example.com", "Anderer Salon");
        var other = new SalonSeeder(world.Host.ConnectionString, otherMe.Salon!.Id);
        var otherCut = await other.AddServiceAsync();
        var otherStaff = await other.AddStaffAsync("Olga", 0, offers: otherCut);
        var foreign = await other.AddAppointmentAsync(otherCut, otherStaff, Tomorrow10, expiresAt: Now.AddHours(5));

        Assert.Equal(HttpStatusCode.NotFound, (await world.Owner.GetAsync(Url(foreign.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Owner.PostAsync(Url(foreign.Id, "confirm"))).StatusCode);
        Assert.Empty(await ReadAsync<List<AppointmentDto>>(await world.Owner.GetAsync("/api/appointments/requests")));
        Assert.Equal(1, (await ReadAsync<OpenRequestCount>(await otherOwner.GetAsync("/api/appointments/requests/count"))).Count);
    }

    // ------------------------------------------------------------ answering

    [Fact]
    public async Task Confirming_a_request_confirms_it_tells_the_customer_and_is_written_to_the_audit_log()
    {
        await using var world = await NewWorldAsync();
        var request = await world.AddAsync();

        var dto = await ReadAsync<AppointmentDto>(await world.Owner.PostAsync(Url(request.Id, "confirm")));

        Assert.Equal(AppointmentStatus.Confirmed, dto.Status);
        Assert.Null(dto.ExpiresAtUtc);
        Assert.Equal([AppointmentStatus.Cancelled, AppointmentStatus.Completed, AppointmentStatus.NoShow], dto.NextStates);
        await world.Salon.WithDbAsync(async db =>
        {
            var stored = await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == request.Id);
            Assert.Equal(Now, stored.ConfirmedAtUtc);
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingConfirmed);
            var payload = JsonDocument.Parse(mail.PayloadJson!).RootElement;
            Assert.Equal(request.Id, payload.GetProperty("AppointmentId").GetGuid());
            Assert.Equal("Anna", payload.GetProperty("StaffName").GetString());
            Assert.Contains("/appointment/", payload.GetProperty("ManageUrl").GetString());
            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "appointment.confirmed");
            Assert.Equal((world.OwnerUserId, nameof(Appointment), request.Id), (audit.ActorUserId, audit.EntityType, audit.EntityId));
            Assert.Equal("""{"from":"Requested","to":"Confirmed","hasReason":false}""", audit.Details);
            return 0;
        });
    }

    [Fact]
    public async Task Declining_with_a_reason_tells_the_customer_but_keeps_the_reason_out_of_the_audit_log()
    {
        await using var world = await NewWorldAsync();
        var request = await world.AddAsync();

        var dto = await ReadAsync<AppointmentDto>(await world.Owner.PostAsync(Url(request.Id, "decline"), new ReasonRequest("Wir haben Betriebsausflug.")));

        Assert.Equal(AppointmentStatus.Declined, dto.Status);
        await world.Salon.WithDbAsync(async db =>
        {
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingDeclined);
            Assert.Equal("Wir haben Betriebsausflug.", JsonDocument.Parse(mail.PayloadJson!).RootElement.GetProperty("Reason").GetString());
            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "appointment.declined");
            Assert.DoesNotContain("Betriebsausflug", audit.Details);
            Assert.Contains("\"hasReason\":true", audit.Details);
            return 0;
        });
    }

    [Fact]
    public async Task Declining_without_a_reason_works_and_the_mail_has_none()
    {
        await using var world = await NewWorldAsync();
        var request = await world.AddAsync();

        Assert.Equal(HttpStatusCode.OK, (await world.Owner.PostAsync(Url(request.Id, "decline"), new { })).StatusCode);

        await world.Salon.WithDbAsync(async db =>
        {
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingDeclined);
            Assert.False(JsonDocument.Parse(mail.PayloadJson!).RootElement.TryGetProperty("Reason", out var reason) && reason.ValueKind == JsonValueKind.String);
            return 0;
        });
    }

    [Fact]
    public async Task A_reason_that_is_too_long_is_refused_and_nothing_changes()
    {
        await using var world = await NewWorldAsync();
        var request = await world.AddAsync();

        var response = await world.Owner.PostAsync(Url(request.Id, "decline"), new ReasonRequest(new string('x', 501)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.too_long", await response.ProblemCodeAsync());
        Assert.Equal(AppointmentStatus.Requested, (await ReadAsync<AppointmentDto>(await world.Owner.GetAsync(Url(request.Id)))).Status);
    }

    [Fact]
    public async Task A_request_whose_time_to_answer_is_over_cannot_be_confirmed_but_can_still_be_declined()
    {
        await using var world = await NewWorldAsync();
        var late = await world.AddAsync(expiresAt: Now.AddMinutes(-1));

        var confirm = await world.Owner.PostAsync(Url(late.Id, "confirm"));

        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("appointment.request_expired", await confirm.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await world.Owner.PostAsync(Url(late.Id, "decline"), new { })).StatusCode);
    }

    [Fact]
    public async Task An_answered_request_cannot_be_answered_again_and_nothing_is_mailed_twice()
    {
        await using var world = await NewWorldAsync();
        var request = await world.AddAsync();
        await world.Owner.PostAsync(Url(request.Id, "confirm"));

        foreach (var action in new[] { "confirm", "decline" })
        {
            var again = await world.Owner.PostAsync(Url(request.Id, action), new { });
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal("appointment.invalid_transition", await again.ProblemCodeAsync());
        }

        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Single(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingConfirmed).ToListAsync());
            Assert.Empty(await db.EmailOutbox.Where(m => m.Type == EmailTypes.BookingDeclined).ToListAsync());
            return 0;
        });
    }

    [Fact]
    public async Task Two_people_answering_at_the_same_moment_cannot_both_succeed()
    {
        await using var world = await NewWorldAsync();
        var request = await world.AddAsync();
        var second = world.Host.NewSession();
        await second.PostAsync("/api/auth/login", new { email = "maria@example.com", password = AuthTestHost.Password });

        var responses = await Task.WhenAll(world.Owner.PostAsync(Url(request.Id, "confirm")), second.PostAsync(Url(request.Id, "decline"), new { }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await world.Salon.WithDbAsync(async db =>
        {
            var mails = await db.EmailOutbox.Where(m => m.Type.StartsWith("booking.")).ToListAsync();
            Assert.Single(mails);
            return 0;
        });
    }

    // ------------------------------------------------------------ cancelling

    [Fact]
    public async Task Cancelling_frees_the_time_at_once_and_tells_the_customer_why()
    {
        await using var world = await NewWorldAsync();
        var confirmed = await world.AddAsync(AppointmentStatus.Confirmed);

        var dto = await ReadAsync<AppointmentDto>(await world.Owner.PostAsync(Url(confirmed.Id, "cancel"), new ReasonRequest("Krankheit")));

        Assert.Equal(AppointmentStatus.Cancelled, dto.Status);
        Assert.Empty(dto.NextStates);
        await world.Salon.WithDbAsync(async db =>
        {
            var mail = await db.EmailOutbox.SingleAsync(m => m.Type == EmailTypes.BookingCancelledBySalon);
            Assert.Equal("Krankheit", JsonDocument.Parse(mail.PayloadJson!).RootElement.GetProperty("Reason").GetString());
            Assert.Equal(Now, (await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == confirmed.Id)).CancelledAtUtc);
            // The same person can be booked for the same time again (the exclusion constraint no longer holds it).
            var again = await world.Salon.AddAppointmentAsync(world.Cut, world.Anna, Tomorrow10, AppointmentStatus.Confirmed);
            Assert.NotEqual(confirmed.Id, again.Id);
            return 0;
        });
    }

    [Theory]
    [InlineData(AppointmentStatus.Declined)]
    [InlineData(AppointmentStatus.Expired)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Finished_appointments_cannot_be_cancelled(AppointmentStatus status)
    {
        await using var world = await NewWorldAsync();
        var done = await world.AddAsync(status, start: Now.AddDays(-1));

        var response = await world.Owner.PostAsync(Url(done.Id, "cancel"), new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("appointment.invalid_transition", await response.ProblemCodeAsync());
    }

    // ------------------------------------------------------------ what happened

    [Theory]
    [InlineData("attended", AppointmentStatus.Completed)]
    [InlineData("no-show", AppointmentStatus.NoShow)]
    public async Task After_the_start_a_confirmed_appointment_is_marked_as_attended_or_missed(string action, AppointmentStatus expected)
    {
        await using var world = await NewWorldAsync();
        var past = await world.AddAsync(AppointmentStatus.Confirmed, start: Now.AddHours(-3));

        var dto = await ReadAsync<AppointmentDto>(await world.Owner.PostAsync(Url(past.Id, action)));

        Assert.Equal(expected, dto.Status);
        await world.Salon.WithDbAsync(async db =>
        {
            var stored = await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == past.Id);
            Assert.Equal(Now, expected == AppointmentStatus.Completed ? stored.CompletedAtUtc : stored.NoShowAtUtc);
            Assert.Empty(await db.EmailOutbox.Where(m => m.Type.StartsWith("booking.")).ToListAsync()); // no mail for this
            Assert.Single(await db.AuditLogs.Where(a => a.Action == (expected == AppointmentStatus.Completed ? "appointment.completed" : "appointment.no_show")).ToListAsync());
            return 0;
        });
    }

    [Theory]
    [InlineData("attended")]
    [InlineData("no-show")]
    public async Task Before_the_start_nobody_can_say_what_happened(string action)
    {
        await using var world = await NewWorldAsync();
        var future = await world.AddAsync(AppointmentStatus.Confirmed);

        var response = await world.Owner.PostAsync(Url(future.Id, action));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("appointment.not_started", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task An_open_request_cannot_be_marked_as_attended()
    {
        await using var world = await NewWorldAsync();
        var request = await world.AddAsync(start: Now.AddHours(-3), expiresAt: Now.AddHours(1));

        var response = await world.Owner.PostAsync(Url(request.Id, "attended"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Appointments_that_are_over_but_not_marked_are_listed_oldest_first()
    {
        await using var world = await NewWorldAsync();
        var newer = await world.AddAsync(AppointmentStatus.Confirmed, start: Now.AddHours(-3));
        var older = await world.AddAsync(AppointmentStatus.Confirmed, start: Now.AddDays(-1));
        await world.AddAsync(AppointmentStatus.Confirmed, start: Now.AddDays(-2), staff: world.Anna); // marked below
        await world.AddAsync(AppointmentStatus.Confirmed, start: Now.AddHours(5)); // still to come
        await world.AddAsync(AppointmentStatus.Completed, start: Now.AddDays(-3));
        var marked = (await ReadAsync<List<AppointmentDto>>(await world.Owner.GetAsync("/api/appointments/awaiting-outcome"))).Single(a => a.StartUtc == Now.AddDays(-2));
        await world.Owner.PostAsync(Url(marked.Id, "attended"));

        var list = await ReadAsync<List<AppointmentDto>>(await world.Owner.GetAsync("/api/appointments/awaiting-outcome"));

        Assert.Equal([older.Id, newer.Id], list.Select(a => a.Id));
    }

    // ------------------------------------------------------------ rights

    [Fact]
    public async Task Staff_see_and_answer_only_their_own_appointments_while_managers_see_all()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        var manager = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Mona", "mona@example.com", SalonRole.Manager);
        var benMember = await world.Salon.WithDbAsync(db => db.StaffMembers.AsNoTracking().SingleAsync(s => s.DisplayName == "Ben"));
        var annas = await world.AddAsync();
        var bens = await world.AddAsync(start: Tomorrow10.AddHours(3), staff: benMember);

        var benList = await ReadAsync<List<AppointmentDto>>(await ben.GetAsync("/api/appointments/requests"));
        var managerList = await ReadAsync<List<AppointmentDto>>(await manager.GetAsync("/api/appointments/requests"));

        Assert.Equal([bens.Id], benList.Select(a => a.Id));
        Assert.Equal(2, managerList.Count);
        Assert.Equal(1, (await ReadAsync<OpenRequestCount>(await ben.GetAsync("/api/appointments/requests/count"))).Count);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.GetAsync(Url(annas.Id))).StatusCode);
        foreach (var action in new[] { "confirm", "decline", "cancel", "attended", "no-show" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await ben.PostAsync(Url(annas.Id, action), new { })).StatusCode);
        }

        Assert.Equal(AppointmentStatus.Requested, (await ReadAsync<AppointmentDto>(await world.Owner.GetAsync(Url(annas.Id)))).Status);
        Assert.Equal(AppointmentStatus.Confirmed, (await ReadAsync<AppointmentDto>(await ben.PostAsync(Url(bens.Id, "confirm")))).Status);
        Assert.Equal(AppointmentStatus.Confirmed, (await ReadAsync<AppointmentDto>(await manager.PostAsync(Url(annas.Id, "confirm")))).Status);
        var audit = await world.Salon.WithDbAsync(db => db.AuditLogs.AsNoTracking().Where(a => a.Action == "appointment.confirmed").Select(a => a.ActorUserId).ToListAsync());
        Assert.Equal(2, audit.Distinct().Count());
        Assert.DoesNotContain(null, audit);
    }

    [Fact]
    public async Task Writes_need_the_antiforgery_token()
    {
        await using var world = await NewWorldAsync();
        var request = await world.AddAsync();
        var plain = world.Host.NewSession();
        (await plain.PostAsync("/api/auth/login", new { email = "maria@example.com", password = AuthTestHost.Password })).EnsureSuccessStatusCode();
        plain.Client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        var response = await plain.Client.PostAsJsonAsync(Url(request.Id, "confirm"), new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AppointmentStatus.Requested, (await ReadAsync<AppointmentDto>(await world.Owner.GetAsync(Url(request.Id)))).Status);
    }
}
