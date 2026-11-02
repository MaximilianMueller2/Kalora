using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kalora.Application.Customers;
using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class CustomersTests(PostgresFixture postgres)
{
    // The test host's clock: Tuesday 3 November 2026, 09:00 UTC.
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private sealed class World : IAsyncDisposable
    {
        public required AuthTestHost Host { get; init; }
        public required ApiSession Owner { get; init; }
        public required SalonSeeder Salon { get; init; }
        public required Service Cut { get; init; }
        public required StaffMember Anna { get; init; }

        public Task<Customer> AddCustomerAsync(string name, string? email = null, string? phone = null, int noShows = 0) => Salon.WithDbAsync(async db =>
        {
            var customer = new Customer { Name = name, Email = email ?? $"{Guid.NewGuid():N}@example.com", Phone = phone, NoShowCount = noShows };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer;
        });

        public Task<Appointment> AddAppointmentAsync(Customer customer, AppointmentStatus status, DateTimeOffset start, string? customerNote = "Allergisch gegen Farbe", string? internalNote = "Stammkundin, mag Tee") =>
            Salon.WithDbAsync(async db =>
            {
                var appointment = new Appointment
                {
                    StaffId = Anna.Id, ServiceId = Cut.Id, CustomerId = customer.Id, StartUtc = start, EndUtc = start.AddHours(1), BlockedStartUtc = start, BlockedEndUtc = start.AddHours(1),
                    Status = status, Source = AppointmentSource.Online, ServiceName = "Haarschnitt", DurationMinutes = 60, PriceCents = 4500,
                    CustomerNote = customerNote, InternalNote = internalNote, ExpiresAtUtc = status == AppointmentStatus.Requested ? start.AddHours(-1) : null,
                };
                appointment.ManageTokenHash = Kalora.Application.Security.TokenHasher.Hash(appointment.Id.ToString("N"));
                db.Appointments.Add(appointment);
                await db.SaveChangesAsync();
                return appointment;
            });

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

    private static Task<HttpResponseMessage> List(ApiSession session, string? q = null) =>
        session.GetAsync(q is null ? "/api/customers" : $"/api/customers?q={Uri.EscapeDataString(q)}");

    // ------------------------------------------------------------ list and detail

    [Fact]
    public async Task Customers_are_listed_by_name_with_visits_and_no_shows()
    {
        await using var world = await NewWorldAsync();
        var berta = await world.AddCustomerAsync("Berta Beck", phone: "+49 170 222", noShows: 2);
        await world.AddCustomerAsync("anna Schmidt");
        await world.AddAppointmentAsync(berta, AppointmentStatus.Completed, Now.AddDays(-10));
        await world.AddAppointmentAsync(berta, AppointmentStatus.NoShow, Now.AddDays(-20));

        var list = await ReadAsync<CustomerListDto>(await List(world.Owner));

        Assert.Equal(["anna Schmidt", "Berta Beck"], list.Items.Select(i => i.Name));
        Assert.False(list.HasMore);
        var row = list.Items[1];
        Assert.Equal((berta.Id, "+49 170 222", 2, 2), (row.Id, row.Phone, row.Appointments, row.NoShows));
        Assert.Equal(0, list.Items[0].Appointments);
    }

    [Fact]
    public async Task Search_finds_a_part_of_name_email_or_phone_regardless_of_case()
    {
        await using var world = await NewWorldAsync();
        await world.AddCustomerAsync("Anna Schmidt", "anna@example.com", "+49 170 111");
        await world.AddCustomerAsync("Bernd Beck", "bernd@web.de", "+49 171 222");

        async Task<string[]> Search(string text) => (await ReadAsync<CustomerListDto>(await List(world.Owner, text))).Items.Select(i => i.Name).ToArray();

        Assert.Equal(["Anna Schmidt"], await Search("SCHMI"));
        Assert.Equal(["Bernd Beck"], await Search("web.de"));
        Assert.Equal(["Anna Schmidt"], await Search("170 111"));
        Assert.Empty(await Search("zzz"));
        Assert.Equal(2, (await Search("a")).Length); // one letter is not a search: everybody
        Assert.Equal(2, (await Search("  ")).Length);
    }

    [Fact]
    public async Task A_long_list_is_cut_after_fifty_and_says_so()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.WithDbAsync(async db =>
        {
            db.Customers.AddRange(Enumerable.Range(0, 51).Select(i => new Customer { Name = $"Kunde {i:D2}", Email = $"k{i}@example.com" }));
            await db.SaveChangesAsync();
            return 0;
        });

        var list = await ReadAsync<CustomerListDto>(await List(world.Owner));

        Assert.Equal((50, true, "Kunde 00"), (list.Items.Count, list.HasMore, list.Items[0].Name));
    }

    [Fact]
    public async Task The_detail_shows_the_history_newest_first_with_both_notes()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck", "berta@example.com", "+49 170 222");
        var older = await world.AddAppointmentAsync(customer, AppointmentStatus.Completed, Now.AddDays(-30), "Ohne Föhn", null);
        var newer = await world.AddAppointmentAsync(customer, AppointmentStatus.Confirmed, Now.AddDays(5));

        var detail = await ReadAsync<CustomerDetailDto>(await world.Owner.GetAsync($"/api/customers/{customer.Id}"));

        Assert.Equal(("Berta Beck", "berta@example.com", "+49 170 222"), (detail.Name, detail.Email, detail.Phone));
        Assert.Equal([newer.Id, older.Id], detail.Appointments.Select(a => a.Id));
        Assert.Equal(("Anna", "Allergisch gegen Farbe", "Stammkundin, mag Tee"), (detail.Appointments[0].StaffName, detail.Appointments[0].CustomerNote, detail.Appointments[0].InternalNote));
        Assert.Equal(("Ohne Föhn", null), (detail.Appointments[1].CustomerNote, detail.Appointments[1].InternalNote));
    }

    [Fact]
    public async Task Another_salons_customers_do_not_exist_for_us()
    {
        await using var world = await NewWorldAsync();
        var (otherOwner, otherMe) = await world.Host.RegisterOwnerAsync("other@example.com", "Anderer Salon");
        var foreign = await new SalonSeeder(world.Host.ConnectionString, otherMe.Salon!.Id).WithDbAsync(async db =>
        {
            var customer = new Customer { Name = "Fremd Kunde", Email = "fremd@example.com" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer;
        });

        Assert.Empty((await ReadAsync<CustomerListDto>(await List(world.Owner))).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Owner.GetAsync($"/api/customers/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Owner.GetAsync($"/api/customers/{foreign.Id}/export.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Owner.PostAsync($"/api/customers/{foreign.Id}/anonymize")).StatusCode);
        Assert.Single((await ReadAsync<CustomerListDto>(await List(otherOwner))).Items);
    }

    [Fact]
    public async Task Only_owners_and_managers_may_see_the_customer_file()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck");
        var staff = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Ben", "ben@example.com", SalonRole.Staff);
        var manager = await world.Host.InviteAndAcceptAsync(world.Owner, world.Salon.SalonId, "Mona", "mona@example.com", SalonRole.Manager);

        foreach (var path in new[] { "/api/customers", $"/api/customers/{customer.Id}", $"/api/customers/{customer.Id}/export.json", $"/api/customers/{customer.Id}/export.csv" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await world.Host.NewSession().GetAsync(path)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync($"/api/customers/{customer.Id}/anonymize")).StatusCode);
    }

    // ------------------------------------------------------------ export

    [Fact]
    public async Task The_JSON_export_has_everything_about_the_customer_and_is_written_to_the_audit_log()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck", "berta@example.com", "+49 170 222", noShows: 1);
        await world.AddAppointmentAsync(customer, AppointmentStatus.Completed, Now.AddDays(-3));

        var response = await world.Owner.GetAsync($"/api/customers/{customer.Id}/export.json");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"customer-{customer.Id:N}.json", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(Now, json.GetProperty("exportedAtUtc").GetDateTimeOffset());
        var data = json.GetProperty("customer");
        Assert.Equal(("Berta Beck", "berta@example.com", "+49 170 222", 1), (data.GetProperty("name").GetString(), data.GetProperty("email").GetString(), data.GetProperty("phone").GetString(), data.GetProperty("noShowCount").GetInt32()));
        var appointment = json.GetProperty("appointments")[0];
        Assert.Equal(("Completed", "Haarschnitt", "Allergisch gegen Farbe", "Stammkundin, mag Tee"), (appointment.GetProperty("status").GetString(), appointment.GetProperty("serviceName").GetString(), appointment.GetProperty("customerNote").GetString(), appointment.GetProperty("internalNote").GetString()));
        await world.Salon.WithDbAsync(async db =>
        {
            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "customer.exported");
            Assert.Equal((nameof(Customer), customer.Id), (audit.EntityType, audit.EntityId));
            Assert.NotNull(audit.ActorUserId);
            Assert.Equal("""{"format":"json"}""", audit.Details);
            Assert.DoesNotContain("berta", audit.Details); // no personal data in the log
            return 0;
        });
    }

    [Fact]
    public async Task The_CSV_export_has_a_row_per_appointment_and_is_safe_to_open_in_a_spreadsheet()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta \"Bea\" Beck", "berta@example.com", "+49 170 222");
        await world.AddAppointmentAsync(customer, AppointmentStatus.Completed, Now.AddDays(-3), "Mit Komma, und\nZeilenumbruch", "=HYPERLINK(\"http://evil\")");
        await world.AddAppointmentAsync(customer, AppointmentStatus.Confirmed, Now.AddDays(3), null, null);

        var response = await world.Owner.GetAsync($"/api/customers/{customer.Id}/export.csv");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]); // byte order mark for spreadsheets
        Assert.StartsWith("﻿appointmentId,status,startUtc,endUtc,service,staff,priceCents,priceIsFrom,currency,customerNote,internalNote,customerName,customerEmail,customerPhone\r\n", text);
        Assert.Contains("\"Berta \"\"Bea\"\" Beck\"", text);
        Assert.Contains("\"Mit Komma, und\nZeilenumbruch\"", text);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\")\"", text); // a formula is defused, quoted, and still readable
        Assert.DoesNotContain(",=HYPERLINK", text);
        Assert.Equal(3, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length); // the header and one row per appointment; the line break inside a note is only \n
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Equal("""{"format":"csv"}""", (await db.AuditLogs.SingleAsync(a => a.Action == "customer.exported")).Details);
            return 0;
        });
    }

    [Fact]
    public async Task A_customer_without_appointments_still_gets_one_row_with_their_data()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck", "berta@example.com");

        var text = await (await world.Owner.GetAsync($"/api/customers/{customer.Id}/export.csv")).Content.ReadAsStringAsync();

        Assert.Equal(2, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.EndsWith(",Berta Beck,berta@example.com,\r\n", text);
    }

    // ------------------------------------------------------------ deleting

    [Fact]
    public async Task Deleting_a_customer_removes_name_email_and_phone_and_leaves_the_appointments_without_a_person()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck", "berta@example.com", "+49 170 222", noShows: 1);
        var past = await world.AddAppointmentAsync(customer, AppointmentStatus.Completed, Now.AddDays(-3));
        var noShow = await world.AddAppointmentAsync(customer, AppointmentStatus.NoShow, Now.AddDays(-8));
        await world.Salon.WithDbAsync(async db =>
        {
            (await db.Customers.SingleAsync()).MarketingConsentAtUtc = Now.AddDays(-100);
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await world.Owner.PostAsync($"/api/customers/{customer.Id}/anonymize");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await world.Salon.WithDbAsync(async db =>
        {
            var stored = await db.Customers.AsNoTracking().SingleAsync();
            Assert.Equal((Customer.AnonymizedName, null, null, null, Now), (stored.Name, stored.Email, stored.Phone, stored.MarketingConsentAtUtc, stored.AnonymizedAtUtc));
            Assert.Equal(1, stored.NoShowCount); // numbers stay
            var appointments = await db.Appointments.AsNoTracking().ToListAsync();
            Assert.Equal(2, appointments.Count); // the appointments stay for the statistics
            Assert.All(appointments, a => Assert.Equal((null, null, null, "Haarschnitt"), (a.CustomerNote, a.InternalNote, a.ManageTokenHash, a.ServiceName)));
            var audit = await db.AuditLogs.SingleAsync(a => a.Action == "customer.anonymized");
            Assert.Equal((customer.Id, "salon"), (audit.EntityId, JsonDocument.Parse(audit.Details!).RootElement.GetProperty("reason").GetString()));
            Assert.NotNull(audit.ActorUserId);
            return 0;
        });
        // The old management link is dead and the customer is gone from list and search.
        Assert.Equal(HttpStatusCode.NotFound, (await world.Host.NewSession().GetAsync($"/api/public/appointments/{past.Id:N}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Host.NewSession().GetAsync($"/api/public/appointments/{noShow.Id:N}")).StatusCode);
        Assert.Empty((await ReadAsync<CustomerListDto>(await List(world.Owner))).Items);
        Assert.Empty(await ReadAsync<List<Kalora.Application.Calendar.CustomerHitDto>>(await world.Owner.GetAsync("/api/calendar/customers?q=Berta")));
    }

    [Fact]
    public async Task Deleting_also_empties_the_mails_about_the_customer_and_gives_up_those_not_yet_sent()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck", "berta@example.com");
        await world.AddCustomerAsync("Otto Ohl", "otto@example.com");
        var appointment = await world.AddAppointmentAsync(customer, AppointmentStatus.Completed, Now.AddDays(-3));
        await world.Salon.WithDbAsync(async db =>
        {
            EmailOutbox Mail(string to, string payload, EmailStatus status) =>
                new() { ToEmail = to, Type = "booking.confirmed", Language = "de", Subject = "Betreff mit Namen", PayloadJson = payload, BodyHtml = "<p>Berta</p>", BodyText = "Berta", Status = status, Attempts = 1 };
            db.EmailOutbox.AddRange(
                Mail("berta@example.com", """{"CustomerName":"Berta Beck"}""", EmailStatus.Sent),
                Mail("berta@example.com", """{"CustomerName":"Berta Beck"}""", EmailStatus.Pending),
                Mail("maria@example.com", $$"""{"AppointmentId":"{{appointment.Id}}","CustomerName":"Berta Beck"}""", EmailStatus.Sent),
                Mail("otto@example.com", """{"CustomerName":"Otto Ohl"}""", EmailStatus.Pending),
                Mail("maria@example.com", $$"""{"AppointmentId":"{{Guid.NewGuid()}}","CustomerName":"Otto Ohl"}""", EmailStatus.Sent));
            await db.SaveChangesAsync();
            return 0;
        });

        await world.Owner.PostAsync($"/api/customers/{customer.Id}/anonymize");

        await world.Salon.WithDbAsync(async db =>
        {
            var mails = await db.EmailOutbox.AsNoTracking().Where(m => m.Type == "booking.confirmed").ToListAsync();
            var hers = mails.Where(m => m.PayloadJson == null).ToList();
            Assert.Equal(3, hers.Count); // two to her, one to the salon about her appointment
            Assert.All(hers, m => Assert.Equal((string.Empty, null, null), (m.Subject, m.BodyHtml, m.BodyText)));
            Assert.Equal(2, hers.Count(m => m.ToEmail == string.Empty));
            Assert.Single(hers, m => m.ToEmail == "maria@example.com"); // the salon's own address stays
            Assert.Equal(EmailStatus.Failed, hers.Single(m => m.Status == EmailStatus.Failed).Status); // the pending one will not be sent
            Assert.Equal(1, hers.Count(m => m.Status == EmailStatus.Failed));
            // What belongs to somebody else stays as it was.
            Assert.Equal(2, mails.Count(m => m.PayloadJson != null));
            Assert.Equal(EmailStatus.Pending, mails.Single(m => m.ToEmail == "otto@example.com").Status);
            return 0;
        });
    }

    [Fact]
    public async Task A_customer_with_an_appointment_still_to_come_cannot_be_deleted_before_it_is_cancelled()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck");
        var future = await world.AddAppointmentAsync(customer, AppointmentStatus.Confirmed, Now.AddDays(2));
        var open = await world.AddAppointmentAsync(customer, AppointmentStatus.Requested, Now.AddDays(4));

        var refused = await world.Owner.PostAsync($"/api/customers/{customer.Id}/anonymize");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("customer.has_open_appointments", await refused.ProblemCodeAsync());
        Assert.Equal("Berta Beck", (await ReadAsync<CustomerDetailDto>(await world.Owner.GetAsync($"/api/customers/{customer.Id}"))).Name);

        await world.Owner.PostAsync($"/api/appointments/{future.Id}/cancel", new { });
        await world.Owner.PostAsync($"/api/appointments/{open.Id}/decline", new { });
        Assert.Equal(HttpStatusCode.NoContent, (await world.Owner.PostAsync($"/api/customers/{customer.Id}/anonymize")).StatusCode);
    }

    [Fact]
    public async Task Deleting_twice_or_an_unknown_customer_is_refused()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck");
        await world.Owner.PostAsync($"/api/customers/{customer.Id}/anonymize");

        var again = await world.Owner.PostAsync($"/api/customers/{customer.Id}/anonymize");
        var unknown = await world.Owner.PostAsync($"/api/customers/{Guid.NewGuid()}/anonymize");

        Assert.Equal("customer.already_anonymized", await again.ProblemCodeAsync());
        Assert.Equal("customer.not_found", await unknown.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_deleted_customer_can_come_back_as_a_new_one_with_the_same_email()
    {
        await using var world = await NewWorldAsync();
        var customer = await world.AddCustomerAsync("Berta Beck", "berta@example.com");
        await world.Owner.PostAsync($"/api/customers/{customer.Id}/anonymize");

        var created = await world.Owner.PostAsync("/api/calendar/appointments", new
        {
            serviceId = world.Cut.Id, staffId = world.Anna.Id, startUtc = new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero), customerName = "Berta Beck", customerEmail = "berta@example.com",
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(2, await world.Salon.WithDbAsync(db => db.Customers.CountAsync()));
    }
}
