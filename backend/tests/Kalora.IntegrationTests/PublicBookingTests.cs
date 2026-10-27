using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kalora.Application.Common;
using Kalora.Application.PublicBooking;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class PublicBookingTests(PostgresFixture postgres)
{
    // Tuesday 3 November 2026, 09:00 UTC = 10:00 Berlin. Salon defaults: 15-minute grid, 2 h lead time, 24 h to answer.
    private static readonly DateOnly Wednesday = new(2026, 11, 4);

    private static DateTimeOffset Berlin(string time, DateOnly? date = null) =>
        new DateTimeOffset((date ?? Wednesday).ToDateTime(TimeOnly.Parse(time)), TimeSpan.FromHours(1)).ToUniversalTime(); // winter time

    private sealed class World : IAsyncDisposable
    {
        public required AuthTestHost Host { get; init; }
        public required SalonSeeder Salon { get; init; }
        public required string Slug { get; init; }
        public required HttpClient Client { get; init; }
        public required Service Cut { get; init; }
        public required StaffMember Anna { get; init; }

        public string Url(string path = "") => $"/api/public/salons/{Slug}{path}";

        public Task<HttpResponseMessage> PostAsync(object body, HttpClient? client = null) => (client ?? Client).PostAsJsonAsync(Url("/appointment-requests"), body);

        public async ValueTask DisposeAsync() => await Host.DisposeAsync();
    }

    private async Task<World> NewWorldAsync(IReadOnlyDictionary<string, string>? settings = null, string email = "maria@example.com")
    {
        var host = await AuthTestHost.StartAsync(postgres, settings);
        var (_, me) = await host.RegisterOwnerAsync(email, "Haarwerk Müller");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        await salon.OpenAsync();
        var cut = await salon.AddServiceAsync(minutes: 60);
        var anna = await salon.AddStaffAsync("Anna", 0, offers: cut);
        return new World { Host = host, Salon = salon, Slug = me.Salon.Slug, Client = host.NewSession().Client, Cut = cut, Anna = anna };
    }

    private static object Booking(Service service, string time = "10:00", string email = "kim@example.com", Guid? staffId = null, string name = "Kim Kundin",
        string? phone = null, string? note = null, string? language = null, string? website = null, DateOnly? date = null) =>
        new { serviceId = service.Id, startUtc = Berlin(time, date), staffId, name, email, phone, note, language, website };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private static async Task<string> CodeAsync(HttpResponseMessage response) => await response.ProblemCodeAsync() ?? "(none)";

    // ---------------------------------------------------------------- what the page shows

    [Fact]
    public async Task The_profile_shows_what_a_customer_needs_and_nothing_internal()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => { s.Street = "Hauptstraße 1"; s.PostalCode = "10115"; s.City = "Berlin"; s.Phone = "+49 30 123456"; s.ImprintText = "Impressum"; s.PrivacyPolicyUrl = "https://example.com/datenschutz"; });

        var response = await world.Client.GetAsync(world.Url());
        var text = await response.Content.ReadAsStringAsync();
        var profile = JsonSerializer.Deserialize<PublicSalonDto>(text, TestJson.Options)!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("Haarwerk Müller", "Berlin", "Europe/Berlin", "de"), (profile.Name, profile.City, profile.TimeZone, profile.DefaultLanguage));
        Assert.Equal(["de", "en", "tr"], profile.Languages);
        Assert.Equal(7, profile.OpeningHours.Count);
        Assert.Equal(DayOfWeek.Monday, profile.OpeningHours[0].Weekday);
        Assert.Equal("Impressum", profile.ImprintText);
        Assert.Equal("https://example.com/datenschutz", profile.PrivacyPolicyUrl);
        foreach (var internalField in new[] { "autoConfirm", "retention", "planCode", "trial", "status", "requestExpiry", "slotInterval", "id\"" })
        {
            Assert.DoesNotContain(internalField, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task An_unknown_suspended_or_cancelled_salon_is_not_found()
    {
        await using var world = await NewWorldAsync();

        Assert.Equal("salon.not_found", await CodeAsync(await world.Client.GetAsync("/api/public/salons/gibt-es-nicht")));
        foreach (var status in new[] { SalonStatus.Suspended, SalonStatus.Cancelled })
        {
            await world.Salon.ConfigureSalonAsync(s => s.Status = status);
            Assert.Equal(HttpStatusCode.NotFound, (await world.Client.GetAsync(world.Url())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await world.Client.GetAsync(world.Url("/services"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await world.PostAsync(Booking(world.Cut))).StatusCode);
        }

        await world.Salon.ConfigureSalonAsync(s => s.Status = SalonStatus.PastDue);
        Assert.Equal(HttpStatusCode.OK, (await world.Client.GetAsync(world.Url())).StatusCode);
    }

    [Fact]
    public async Task Only_active_online_services_that_someone_offers_are_listed_in_category_order_without_buffers()
    {
        await using var world = await NewWorldAsync();
        var color = await world.Salon.AddServiceAsync("Färben", 120, before: 10, after: 20, category: "Damen", priceCents: 8000, priceIsFrom: true, description: "Mit Pflege");
        var beard = await world.Salon.AddServiceAsync("Bart", 30, category: "Herren");
        var inactive = await world.Salon.AddServiceAsync("Alt", active: false);
        var hidden = await world.Salon.AddServiceAsync("Intern", online: false);
        var noOne = await world.Salon.AddServiceAsync("Ohne Person");
        var onlyOffline = await world.Salon.AddServiceAsync("Nur intern gebucht");
        await world.Salon.AddStaffAsync("Ben", 1, offers: [color, beard, inactive, hidden]);
        await world.Salon.AddStaffAsync("Offline", 2, online: false, offers: [onlyOffline]);

        var response = await world.Client.GetAsync(world.Url("/services"));
        var text = await response.Content.ReadAsStringAsync();
        var categories = JsonSerializer.Deserialize<List<PublicCategoryDto>>(text, TestJson.Options)!;

        Assert.Equal(["Damen", "Herren"], categories.Select(c => c.Name));
        Assert.Equal(["Haarschnitt", "Färben"], categories[0].Services.Select(s => s.Name).Order(StringComparer.Ordinal).Reverse());
        var dyed = categories[0].Services.Single(s => s.Name == "Färben");
        Assert.Equal((8000, true, "EUR", 120, "Mit Pflege"), (dyed.PriceCents, dyed.PriceIsFrom, dyed.Currency, dyed.DurationMinutes, dyed.Description));
        Assert.Equal(["Bart"], categories[1].Services.Select(s => s.Name));
        foreach (var leak in new[] { "buffer", "isActive", "isBookableOnline", "sortOrder", "categoryId" })
        {
            Assert.DoesNotContain(leak, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_people_for_a_service_are_shown_with_name_and_color_only()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.AddStaffAsync("Ben", 1, offers: world.Cut);
        await world.Salon.AddStaffAsync("Inaktiv", 2, active: false, offers: world.Cut);
        await world.Salon.AddStaffAsync("Intern", 3, online: false, offers: world.Cut);
        await world.Salon.AddStaffAsync("Kann es nicht", 4);

        var response = await world.Client.GetAsync(world.Url($"/staff?serviceId={world.Cut.Id}"));
        var text = await response.Content.ReadAsStringAsync();
        var people = JsonSerializer.Deserialize<List<PublicStaffDto>>(text, TestJson.Options)!;

        Assert.Equal(["Anna", "Ben"], people.Select(p => p.DisplayName));
        Assert.Equal("#2563EB", people[0].Color);
        using var document = JsonDocument.Parse(text);
        Assert.Equal(["color", "displayName", "id"], document.RootElement[0].EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("service.not_found", await CodeAsync(await world.Client.GetAsync(world.Url($"/staff?serviceId={Guid.NewGuid()}"))));
    }

    [Fact]
    public async Task Days_with_free_times_and_the_start_times_of_a_day_are_listed_without_saying_who_is_free()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.WithDbAsync(async db =>
        {
            db.SalonClosures.Add(new SalonClosure { Date = new DateOnly(2026, 11, 10), Reason = "Fortbildung" });
            await db.SaveChangesAsync();
            return 0;
        });

        var days = await ReadAsync<PublicDaysDto>(await world.Client.GetAsync(world.Url($"/availability/days?serviceId={world.Cut.Id}&month=2026-11")));
        var slotsResponse = await world.Client.GetAsync(world.Url($"/availability/slots?serviceId={world.Cut.Id}&date=2026-11-04"));
        var slotsText = await slotsResponse.Content.ReadAsStringAsync();
        var slots = JsonSerializer.Deserialize<PublicSlotsDto>(slotsText, TestJson.Options)!;

        Assert.Equal("2026-11", days.Month);
        // Today is 3 November at 10:00 local; the lead time of two hours leaves the working time after 12:00 on the 3rd.
        Assert.Equal(new DateOnly(2026, 11, 3), days.Days[0]);
        Assert.DoesNotContain(new DateOnly(2026, 11, 10), days.Days);
        Assert.Equal(27, days.Days.Count); // 3-30 November without the closed 10th
        Assert.Equal(new DateOnly(2026, 11, 30), days.Days[^1]);
        Assert.Equal(Wednesday, slots.Date);
        Assert.Equal("09:00", slots.Slots[0].Time);
        Assert.Equal(Berlin("09:00"), slots.Slots[0].StartUtc);
        Assert.Equal("16:00", slots.Slots[^1].Time);
        Assert.DoesNotContain("staff", slotsText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("availability.month_invalid", await CodeAsync(await world.Client.GetAsync(world.Url($"/availability/days?serviceId={world.Cut.Id}&month=11-2026"))));
    }

    [Fact]
    public async Task A_wished_person_narrows_the_free_times()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Salon.AddStaffAsync("Ben", 1, offers: world.Cut);
        await world.Host.AddBookingAsync(world.Salon.SalonId, ben.Id, world.Cut.Id, Berlin("09:00"), AppointmentStatus.Confirmed, 60);

        var forBen = await ReadAsync<PublicSlotsDto>(await world.Client.GetAsync(world.Url($"/availability/slots?serviceId={world.Cut.Id}&staffId={ben.Id}&date=2026-11-04")));
        var anyone = await ReadAsync<PublicSlotsDto>(await world.Client.GetAsync(world.Url($"/availability/slots?serviceId={world.Cut.Id}&date=2026-11-04")));

        Assert.Equal("10:00", forBen.Slots[0].Time);
        Assert.Equal("09:00", anyone.Slots[0].Time);
        Assert.Equal("staff.not_found", await CodeAsync(await world.Client.GetAsync(world.Url($"/availability/slots?serviceId={world.Cut.Id}&staffId={Guid.NewGuid()}&date=2026-11-04"))));
    }

    // ---------------------------------------------------------------- requesting an appointment

    [Fact]
    public async Task A_request_stores_everything_in_one_go_and_queues_the_mails()
    {
        await using var world = await NewWorldAsync();
        var service = await world.Salon.AddServiceAsync("Färben", 90, before: 15, after: 30, priceCents: 8000, priceIsFrom: true);
        await world.Salon.WithDbAsync(async db =>
        {
            db.StaffServices.Add(new StaffService { StaffId = world.Anna.Id, ServiceId = service.Id, DurationOverrideMinutes = 120 });
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await world.PostAsync(Booking(service, "10:00", "  Kim@Example.COM ", name: " Kim Kundin ", phone: "+49 170 1234567", note: " Bitte kurz ", language: "en"));
        var receipt = await ReadAsync<BookingReceipt>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((AppointmentStatus.Requested, "Färben", "Anna"), (receipt.Status, receipt.ServiceName, receipt.StaffName));
        Assert.Equal(Berlin("10:00"), receipt.StartUtc);
        Assert.Equal(Berlin("12:00"), receipt.EndUtc); // Anna needs two hours for this service
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero), receipt.RespondByUtc); // 24 hours after "now"
        Assert.False(string.IsNullOrEmpty(receipt.ManageToken));

        await world.Salon.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync();
            var appointment = await db.Appointments.SingleAsync();
            Assert.Equal(("kim@example.com", "Kim Kundin", "+49 170 1234567", "en"), (customer.Email, customer.Name, customer.Phone, customer.PreferredLanguage));
            Assert.Equal((world.Anna.Id, service.Id, customer.Id, AppointmentStatus.Requested, AppointmentSource.Online), (appointment.StaffId, appointment.ServiceId, appointment.CustomerId, appointment.Status, appointment.Source));
            Assert.Equal(("Färben", 8000, true, "EUR", 120, "en", "Bitte kurz"), (appointment.ServiceName, appointment.PriceCents, appointment.PriceIsFrom, appointment.Currency, appointment.DurationMinutes, appointment.Language, appointment.CustomerNote));
            Assert.Equal((Berlin("10:00"), Berlin("12:00")), (appointment.StartUtc, appointment.EndUtc));
            Assert.Equal((Berlin("10:00").AddMinutes(-15), Berlin("12:00").AddMinutes(30)), (appointment.BlockedStartUtc, appointment.BlockedEndUtc));
            Assert.Equal(receipt.RespondByUtc, appointment.ExpiresAtUtc);
            Assert.Null(appointment.ConfirmedAtUtc);
            // Only the hash of the management token is stored.
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(receipt.ManageToken))), appointment.ManageTokenHash);
            Assert.NotEqual(receipt.ManageToken, appointment.ManageTokenHash);

            var mails = await db.EmailOutbox.Where(m => m.Type.StartsWith("booking.")).OrderBy(m => m.Type).ToListAsync();
            Assert.Equal([EmailTypes.BookingNewForSalon, EmailTypes.BookingRequestReceived], mails.Select(m => m.Type));
            var toCustomer = mails.Single(m => m.Type == EmailTypes.BookingRequestReceived);
            Assert.Equal(("kim@example.com", "en"), (toCustomer.ToEmail, toCustomer.Language));
            Assert.Contains($"/{world.Slug}/appointment/{receipt.ManageToken}", toCustomer.PayloadJson);
            var toSalon = mails.Single(m => m.Type == EmailTypes.BookingNewForSalon);
            Assert.Equal(("maria@example.com", "de"), (toSalon.ToEmail, toSalon.Language));
            Assert.Contains("170 1234567", toSalon.PayloadJson);
            Assert.DoesNotContain(receipt.ManageToken, toSalon.PayloadJson);
            return 0;
        });
    }

    [Fact]
    public async Task The_response_time_never_runs_past_the_start_of_the_appointment()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => { s.RequestExpiryHours = 72; s.MinLeadTimeMinutes = 0; });

        // Today at 12:00 local, i.e. three hours from "now" (10:00 local): the salon has until then, not 72 hours.
        var receipt = await ReadAsync<BookingReceipt>(await world.PostAsync(Booking(world.Cut, "12:00", date: new DateOnly(2026, 11, 3))));

        Assert.Equal(Berlin("12:00", new DateOnly(2026, 11, 3)), receipt.RespondByUtc);
    }

    [Fact]
    public async Task With_automatic_confirmation_the_appointment_is_confirmed_at_once_and_the_customer_gets_the_confirmation()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.AutoConfirm = true);

        var receipt = await ReadAsync<BookingReceipt>(await world.PostAsync(Booking(world.Cut)));

        Assert.Equal(AppointmentStatus.Confirmed, receipt.Status);
        Assert.Null(receipt.RespondByUtc);
        await world.Salon.WithDbAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync();
            Assert.Equal((AppointmentStatus.Confirmed, null), (appointment.Status, appointment.ExpiresAtUtc));
            Assert.Equal(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), appointment.ConfirmedAtUtc);
            Assert.Contains(EmailTypes.BookingConfirmed, (await db.EmailOutbox.ToListAsync()).Select(m => m.Type));
            Assert.DoesNotContain(EmailTypes.BookingRequestReceived, (await db.EmailOutbox.ToListAsync()).Select(m => m.Type));
            return 0;
        });
    }

    [Fact]
    public async Task A_person_who_confirms_automatically_does_so_even_if_the_salon_does_not()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Salon.AddStaffAsync("Ben", 1, autoConfirm: true, offers: world.Cut);

        var forAnna = await ReadAsync<BookingReceipt>(await world.PostAsync(Booking(world.Cut, staffId: world.Anna.Id)));
        var forBen = await ReadAsync<BookingReceipt>(await world.PostAsync(Booking(world.Cut, email: "lee@example.com", staffId: ben.Id)));

        Assert.Equal((AppointmentStatus.Requested, AppointmentStatus.Confirmed), (forAnna.Status, forBen.Status));
    }

    [Fact]
    public async Task For_anyone_the_person_with_the_fewest_appointments_that_day_is_taken()
    {
        await using var world = await NewWorldAsync();
        var ben = await world.Salon.AddStaffAsync("Ben", 1, offers: world.Cut);
        await world.Host.AddBookingAsync(world.Salon.SalonId, world.Anna.Id, world.Cut.Id, Berlin("15:00"), AppointmentStatus.Confirmed, 60);

        var first = await ReadAsync<BookingReceipt>(await world.PostAsync(Booking(world.Cut, "10:00")));
        var second = await ReadAsync<BookingReceipt>(await world.PostAsync(Booking(world.Cut, "11:00", email: "lee@example.com")));
        var third = await ReadAsync<BookingReceipt>(await world.PostAsync(Booking(world.Cut, "12:00", email: "sam@example.com")));

        // Anna has one appointment, Ben none: Ben first; then both have one and Anna's column comes first; then Ben again.
        Assert.Equal(["Ben", "Anna", "Ben"], new[] { first.StaffName, second.StaffName, third.StaffName });
        Assert.Equal(ben.Id, await world.Salon.WithDbAsync(db => db.Appointments.Where(a => a.StartUtc == Berlin("10:00")).Select(a => a.StaffId).SingleAsync()));
    }

    [Fact]
    public async Task A_returning_customer_is_found_by_e_mail_regardless_of_case_and_keeps_one_record()
    {
        await using var world = await NewWorldAsync();
        await world.PostAsync(Booking(world.Cut, "10:00", "kim@example.com", name: "Kim K.", phone: "+49 170 1"));

        var second = await world.PostAsync(Booking(world.Cut, "13:00", "KIM@example.com", name: "Kim Kundin"));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        await world.Salon.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync();
            Assert.Equal(("Kim Kundin", "+49 170 1"), (customer.Name, customer.Phone)); // an omitted phone number does not erase the old one
            Assert.Equal(2, await db.Appointments.CountAsync(a => a.CustomerId == customer.Id));
            return 0;
        });
    }

    [Fact]
    public async Task A_time_that_is_already_taken_is_reported_as_unavailable()
    {
        await using var world = await NewWorldAsync();
        await world.PostAsync(Booking(world.Cut, "10:00", staffId: world.Anna.Id));

        var overlapping = await world.PostAsync(Booking(world.Cut, "10:30", "lee@example.com", staffId: world.Anna.Id));
        var sameTime = await world.PostAsync(Booking(world.Cut, "10:00", "sam@example.com"));

        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        Assert.Equal("booking.slot_unavailable", await CodeAsync(overlapping));
        Assert.Equal("booking.slot_unavailable", await CodeAsync(sameTime));
        // The failed requests leave nothing behind: no customer, no mail.
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Customers.CountAsync());
            Assert.Equal(2, await db.EmailOutbox.CountAsync(m => m.Type.StartsWith("booking.")));
            return 0;
        });
    }

    [Theory]
    [InlineData("10:10", "off the grid")]
    [InlineData("09:00", "before the lead time")]
    [InlineData("17:00", "after working hours")]
    [InlineData("16:30", "would end after working hours")]
    public async Task Start_times_that_are_not_offered_are_refused(string time, string why)
    {
        await using var world = await NewWorldAsync();
        if (time == "09:00")
        {
            await world.Salon.ConfigureSalonAsync(s => s.MinLeadTimeMinutes = 24 * 60);
        }

        var response = await world.PostAsync(Booking(world.Cut, time));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True("booking.slot_unavailable" == await CodeAsync(response), why);
    }

    [Fact]
    public async Task At_most_three_requests_per_e_mail_may_wait_for_an_answer()
    {
        await using var world = await NewWorldAsync();
        foreach (var time in new[] { "10:00", "11:00", "12:00" })
        {
            Assert.Equal(HttpStatusCode.Created, (await world.PostAsync(Booking(world.Cut, time))).StatusCode);
        }

        var fourth = await world.PostAsync(Booking(world.Cut, "13:00"));
        var otherPerson = await world.PostAsync(Booking(world.Cut, "13:00", "lee@example.com"));
        await world.Salon.WithDbAsync(async db =>
        {
            var first = await db.Appointments.OrderBy(a => a.StartUtc).FirstAsync();
            first.Status = AppointmentStatus.Declined;
            await db.SaveChangesAsync();
            return 0;
        });
        var afterAnswer = await world.PostAsync(Booking(world.Cut, "14:00"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, fourth.StatusCode);
        Assert.Equal("booking.too_many_open_requests", await CodeAsync(fourth));
        Assert.Equal(HttpStatusCode.Created, otherPerson.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterAnswer.StatusCode);
    }

    [Fact]
    public async Task Confirmed_appointments_do_not_count_towards_the_limit_of_open_requests()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.AutoConfirm = true);

        foreach (var time in new[] { "10:00", "11:00", "12:00", "13:00", "14:00" })
        {
            Assert.Equal(HttpStatusCode.Created, (await world.PostAsync(Booking(world.Cut, time))).StatusCode);
        }
    }

    [Theory]
    [InlineData("", "kim@example.com", null, null, "validation.required")]
    [InlineData("Kim", "kein-mail", null, null, "validation.email_invalid")]
    [InlineData("Kim", "", null, null, "validation.required")]
    [InlineData("Kim", "kim@example.com", "call me", null, "settings.phone_invalid")]
    [InlineData("Kim", "kim@example.com", null, "fr", "validation.language_unsupported")]
    public async Task Invalid_requests_are_rejected_with_a_translatable_code(string name, string email, string? phone, string? language, string code)
    {
        await using var world = await NewWorldAsync();

        var response = await world.PostAsync(Booking(world.Cut, name: name, email: email, phone: phone, language: language));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await CodeAsync(response));
    }

    [Fact]
    public async Task Too_long_names_and_notes_are_rejected_and_the_salons_language_is_the_default()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.DefaultLanguage = "tr");

        var longName = await world.PostAsync(Booking(world.Cut, name: new string('x', 101)));
        var longNote = await world.PostAsync(Booking(world.Cut, note: new string('x', 1001)));
        var ok = await world.PostAsync(Booking(world.Cut));

        Assert.Equal("validation.too_long", await CodeAsync(longName));
        Assert.Equal("validation.too_long", await CodeAsync(longNote));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal("tr", await world.Salon.WithDbAsync(db => db.Appointments.Select(a => a.Language).SingleAsync()));
    }

    [Fact]
    public async Task A_filled_honeypot_looks_successful_but_stores_nothing()
    {
        await using var world = await NewWorldAsync();

        var response = await world.PostAsync(Booking(world.Cut, website: "https://spam.example"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Equal((0, 0, 0), (await db.Customers.CountAsync(), await db.Appointments.CountAsync(), await db.EmailOutbox.CountAsync(m => m.Type.StartsWith("booking."))));
            return 0;
        });
    }

    [Fact]
    public async Task A_service_or_person_that_is_not_bookable_online_cannot_be_requested()
    {
        await using var world = await NewWorldAsync();
        var hidden = await world.Salon.AddServiceAsync("Intern", online: false);
        var offline = await world.Salon.AddStaffAsync("Offline", 1, online: false, offers: [hidden, world.Cut]);

        var hiddenService = await world.PostAsync(Booking(hidden));
        var offlinePerson = await world.PostAsync(Booking(world.Cut, staffId: offline.Id));
        var unknownService = await world.PostAsync(new { serviceId = Guid.NewGuid(), startUtc = Berlin("10:00"), name = "Kim", email = "kim@example.com" });

        Assert.Equal("service.not_found", await CodeAsync(hiddenService));
        Assert.Equal("staff.not_found", await CodeAsync(offlinePerson));
        Assert.Equal("service.not_found", await CodeAsync(unknownService));
    }

    // ---------------------------------------------------------------- double bookings

    [Fact]
    public async Task Parallel_requests_for_the_same_time_and_person_have_exactly_one_winner()
    {
        await using var world = await NewWorldAsync();
        var clients = Enumerable.Range(0, 8).Select(_ => world.Host.NewSession().Client).ToList();

        var responses = await Task.WhenAll(clients.Select((client, index) =>
            world.PostAsync(Booking(world.Cut, "10:00", $"kunde{index}@example.com", world.Anna.Id, $"Kunde {index}"), client)));

        var codes = await Task.WhenAll(responses.Select(async r => r.IsSuccessStatusCode ? "created" : await CodeAsync(r)));
        Assert.True(codes.Count(c => c == "created") == 1 && codes.Count(c => c == "booking.slot_unavailable") == 7, string.Join(" | ", codes));
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Appointments.CountAsync());
            // The losers' customers and mails were rolled back with their transactions.
            Assert.Equal(1, await db.Customers.CountAsync());
            Assert.Equal(2, await db.EmailOutbox.CountAsync(m => m.Type.StartsWith("booking.")));
            return 0;
        });
    }

    [Fact]
    public async Task Parallel_requests_for_anyone_fill_every_free_person_and_the_rest_are_turned_away()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.AddStaffAsync("Ben", 1, offers: world.Cut);
        await world.Salon.AddStaffAsync("Cem", 2, offers: world.Cut);
        var clients = Enumerable.Range(0, 8).Select(_ => world.Host.NewSession().Client).ToList();

        var responses = await Task.WhenAll(clients.Select((client, index) =>
            world.PostAsync(Booking(world.Cut, "10:00", $"kunde{index}@example.com", null, $"Kunde {index}"), client)));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.True(created == 3, string.Join(" | ", await Task.WhenAll(responses.Select(async r => $"{(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}"))));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Equal(3, await db.Appointments.Select(a => a.StaffId).Distinct().CountAsync());
            Assert.Equal(3, await db.Customers.CountAsync());
            return 0;
        });
    }

    [Fact]
    public async Task Parallel_requests_of_one_new_customer_for_different_times_create_one_customer()
    {
        await using var world = await NewWorldAsync();
        var times = new[] { "10:00", "11:00", "12:00" };
        var clients = times.Select(_ => world.Host.NewSession().Client).ToList();

        var responses = await Task.WhenAll(clients.Select((client, index) => world.PostAsync(Booking(world.Cut, times[index]), client)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        await world.Salon.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Customers.CountAsync());
            Assert.Equal(3, await db.Appointments.CountAsync());
            return 0;
        });
    }

    // ---------------------------------------------------------------- limits, privacy, tenants

    [Fact]
    public async Task Too_many_requests_from_one_address_are_slowed_down_per_kind_of_endpoint()
    {
        var settings = new Dictionary<string, string>
        {
            ["RateLimiting:Booking:PermitLimit"] = "3",
            ["RateLimiting:Availability:PermitLimit"] = "4",
            ["RateLimiting:Read:PermitLimit"] = "5",
        };
        await using var world = await NewWorldAsync(settings);

        var bookings = new List<HttpResponseMessage>();
        for (var i = 0; i < 4; i++)
        {
            bookings.Add(await world.PostAsync(Booking(world.Cut, "10:00", "", name: "")));
        }

        var slotCodes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            slotCodes.Add((await world.Client.GetAsync(world.Url($"/availability/slots?serviceId={world.Cut.Id}&date=2026-11-04"))).StatusCode);
        }

        var reads = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            reads.Add((await world.Client.GetAsync(world.Url())).StatusCode);
        }

        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests], bookings.Select(b => b.StatusCode));
        Assert.Equal("rate_limited", await CodeAsync(bookings[3]));
        Assert.True(bookings[3].Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], slotCodes);
        Assert.Equal(5, reads.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.TooManyRequests, reads[^1]);
    }

    [Fact]
    public async Task No_public_answer_contains_data_of_other_customers()
    {
        await using var world = await NewWorldAsync();
        await world.Salon.ConfigureSalonAsync(s => s.AutoConfirm = true);
        await world.PostAsync(Booking(world.Cut, "10:00", "geheim@example.com", name: "Geheime Person", phone: "+49 170 9999999", note: "Privatnotiz"));
        var urls = new[]
        {
            world.Url(), world.Url("/services"), world.Url($"/staff?serviceId={world.Cut.Id}"),
            world.Url($"/availability/days?serviceId={world.Cut.Id}&month=2026-11"),
            world.Url($"/availability/slots?serviceId={world.Cut.Id}&date=2026-11-04"),
            world.Url($"/availability/slots?serviceId={world.Cut.Id}&staffId={world.Anna.Id}&date=2026-11-04"),
        };

        foreach (var url in urls)
        {
            var text = await (await world.Client.GetAsync(url)).Content.ReadAsStringAsync();
            foreach (var secret in new[] { "geheim", "Geheime", "9999999", "Privatnotiz", "maria@example.com" })
            {
                Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        // The slot that is booked simply disappears from the list.
        var slots = await ReadAsync<PublicSlotsDto>(await world.Client.GetAsync(world.Url($"/availability/slots?serviceId={world.Cut.Id}&date=2026-11-04")));
        Assert.DoesNotContain("10:00", slots.Slots.Select(s => s.Time));
    }

    [Fact]
    public async Task A_salon_cannot_be_booked_through_the_page_of_another_salon()
    {
        await using var world = await NewWorldAsync(email: "a@example.com");
        var (_, meB) = await world.Host.RegisterOwnerAsync("b@example.com", "Salon B");
        var salonB = new SalonSeeder(world.Host.ConnectionString, meB.Salon!.Id);
        await salonB.OpenAsync();
        var serviceB = await salonB.AddServiceAsync("Nur bei B");
        var staffB = await salonB.AddStaffAsync("Bea", 0, offers: serviceB);

        var servicesA = await world.Client.GetStringAsync(world.Url("/services"));
        var foreignService = await world.PostAsync(Booking(serviceB));
        var foreignStaff = await world.PostAsync(Booking(world.Cut, staffId: staffB.Id));
        var foreignSlots = await world.Client.GetAsync(world.Url($"/availability/slots?serviceId={serviceB.Id}&date=2026-11-04"));

        Assert.DoesNotContain("Nur bei B", servicesA);
        Assert.Equal("service.not_found", await CodeAsync(foreignService));
        Assert.Equal("staff.not_found", await CodeAsync(foreignStaff));
        Assert.Equal("service.not_found", await CodeAsync(foreignSlots));
        await salonB.WithDbAsync(async db =>
        {
            Assert.Equal(0, await db.Appointments.CountAsync());
            return 0;
        });
    }

    [Fact]
    public async Task Public_requests_work_even_with_a_stale_session_cookie_of_a_signed_in_salon()
    {
        await using var world = await NewWorldAsync();
        var (owner, _) = await world.Host.RegisterOwnerAsync("other@example.com", "Anderer Salon");

        // A signed-in owner of another salon opens this salon's public page: the URL decides, not the cookie.
        var profile = await ReadAsync<PublicSalonDto>(await owner.GetAsync(world.Url()));
        var booked = await owner.Client.PostAsJsonAsync(world.Url("/appointment-requests"), Booking(world.Cut));

        Assert.Equal("Haarwerk Müller", profile.Name);
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
    }
}
