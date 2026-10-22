using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Salons;
using Kalora.Domain.Salons;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class SalonSettingsTests(PostgresFixture postgres)
{
    private const string Profile = "/api/settings/profile";
    private const string Rules = "/api/settings/booking-rules";

    private static SalonProfile ValidProfile(SalonProfile current) => current with
    {
        Name = "Haarwerk Müller & Söhne",
        Street = "Hauptstraße 1",
        PostalCode = "10115",
        City = "Berlin",
        Phone = "+49 30 123456",
        TimeZone = "Europe/Berlin",
        DefaultLanguage = "tr",
        ImprintText = "Haarwerk Müller, Hauptstraße 1, Berlin",
        PrivacyPolicyUrl = "https://haarwerk.example/datenschutz",
    };

    [Fact]
    public async Task A_new_salon_starts_with_sensible_defaults()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");

        var profile = await owner.GetJsonAsync<SalonProfile>(Profile);
        var rules = await owner.GetJsonAsync<BookingRules>(Rules);

        Assert.Equal(("Haarwerk Müller", me.Salon!.Slug, SalonIndustry.Hair, "Europe/Berlin", "de"),
            (profile.Name, profile.Slug, profile.Industry, profile.TimeZone, profile.DefaultLanguage));
        Assert.Equal(new BookingRules(15, 120, 60, 24, false, 24, 24), rules);
    }

    [Fact]
    public async Task The_owner_can_update_the_profile_and_the_change_persists()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var update = ValidProfile(await owner.GetJsonAsync<SalonProfile>(Profile)) with { Name = "  Haarwerk Müller & Söhne  ", Phone = " +49 30 123456 " };

        var response = await owner.PutAsync(Profile, update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await owner.GetJsonAsync<SalonProfile>(Profile);
        Assert.Equal(ValidProfile(stored), stored);
    }

    [Fact]
    public async Task Blank_optional_fields_are_stored_as_empty()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        await owner.PutAsync(Profile, ValidProfile(await owner.GetJsonAsync<SalonProfile>(Profile)));

        await owner.PutAsync(Profile, (await owner.GetJsonAsync<SalonProfile>(Profile)) with { ImprintText = "  ", PrivacyPolicyUrl = "" });

        var stored = await owner.GetJsonAsync<SalonProfile>(Profile);
        Assert.Null(stored.ImprintText);
        Assert.Null(stored.PrivacyPolicyUrl);
    }

    [Theory]
    [InlineData("Ab", "settings.slug_invalid")]
    [InlineData("-salon-", "settings.slug_invalid")]
    [InlineData("two  words", "settings.slug_invalid")]
    [InlineData("schön", "settings.slug_invalid")]
    [InlineData("double--dash", "settings.slug_invalid")]
    [InlineData("admin", "settings.slug_reserved")]
    [InlineData("api", "settings.slug_reserved")]
    public async Task Invalid_or_reserved_slugs_are_rejected(string slug, string expectedCode)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var response = await owner.PutAsync(Profile, (await owner.GetJsonAsync<SalonProfile>(Profile)) with { Slug = slug });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_slug_used_by_another_salon_is_a_conflict_and_a_free_one_works()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("a@example.com", "Salon A");
        var (_, other) = await host.RegisterOwnerAsync("b@example.com", "Salon B");

        var taken = await owner.PutAsync(Profile, (await owner.GetJsonAsync<SalonProfile>(Profile)) with { Slug = other.Salon!.Slug });
        var free = await owner.PutAsync(Profile, (await owner.GetJsonAsync<SalonProfile>(Profile)) with { Slug = "salon-a-neu" });

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("settings.slug_taken", await taken.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, free.StatusCode);
    }

    [Theory]
    [InlineData("TimeZone", "Mars/Olympus", "settings.timezone_invalid")]
    [InlineData("TimeZone", "W. Europe Standard Time", "settings.timezone_invalid")]
    [InlineData("DefaultLanguage", "fr", "validation.language_unsupported")]
    [InlineData("Phone", "call me maybe", "settings.phone_invalid")]
    [InlineData("PrivacyPolicyUrl", "javascript:alert(1)", "settings.url_invalid")]
    [InlineData("PrivacyPolicyUrl", "datenschutz", "settings.url_invalid")]
    [InlineData("Name", "", "validation.required")]
    public async Task Invalid_profile_fields_are_rejected_with_a_translatable_code(string field, string value, string expectedCode)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var current = await owner.GetJsonAsync<SalonProfile>(Profile);
        var invalid = field switch
        {
            "TimeZone" => current with { TimeZone = value },
            "DefaultLanguage" => current with { DefaultLanguage = value },
            "Phone" => current with { Phone = value },
            "PrivacyPolicyUrl" => current with { PrivacyPolicyUrl = value },
            _ => current with { Name = value },
        };

        var response = await owner.PutAsync(Profile, invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Managers_can_edit_the_profile_but_not_the_slug_and_staff_cannot_open_settings()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var manager = await host.InviteAndAcceptAsync(owner, me.Salon!.Id, "Mia", "mia@example.com", SalonRole.Manager);
        var staff = await host.InviteAndAcceptAsync(owner, me.Salon.Id, "Tom", "tom@example.com", SalonRole.Staff);
        var current = await manager.GetJsonAsync<SalonProfile>(Profile);

        var rename = await manager.PutAsync(Profile, current with { Name = "Neuer Name" });
        var newSlug = await manager.PutAsync(Profile, current with { Slug = "neuer-slug" });
        var sameSlugAgain = await manager.PutAsync(Profile, current with { Name = "Noch ein Name" });

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, newSlug.StatusCode);
        Assert.Equal("settings.slug_owner_only", await newSlug.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, sameSlugAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(Profile)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(Rules)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewSession().GetAsync(Profile)).StatusCode);
    }

    [Fact]
    public async Task One_salon_cannot_change_the_settings_of_another()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (ownerA, _) = await host.RegisterOwnerAsync("a@example.com", "Salon A");
        var (ownerB, _) = await host.RegisterOwnerAsync("b@example.com", "Salon B");

        await ownerA.PutAsync(Profile, (await ownerA.GetJsonAsync<SalonProfile>(Profile)) with { Name = "Salon A umbenannt" });
        await ownerA.PutAsync(Rules, new BookingRules(30, 60, 30, 12, true, 12, 12));

        Assert.Equal("Salon B", (await ownerB.GetJsonAsync<SalonProfile>(Profile)).Name);
        Assert.Equal(new BookingRules(15, 120, 60, 24, false, 24, 24), await ownerB.GetJsonAsync<BookingRules>(Rules));
    }

    [Fact]
    public async Task Writing_requests_need_the_antiforgery_token()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var response = await owner.Client.PutAsJsonAsync(Profile, await owner.GetJsonAsync<SalonProfile>(Profile));

        Assert.Equal("security.antiforgery_failed", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Booking_rules_can_be_updated()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var update = new BookingRules(30, 240, 90, 48, true, 12, 36);

        var response = await owner.PutAsync(Rules, update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(update, await owner.GetJsonAsync<BookingRules>(Rules));
    }

    [Theory]
    [InlineData(7, 120, 60, 24, 24, 24)]
    [InlineData(15, -1, 60, 24, 24, 24)]
    [InlineData(15, 120, 0, 24, 24, 24)]
    [InlineData(15, 120, 366, 24, 24, 24)]
    [InlineData(15, 120, 60, -1, 24, 24)]
    [InlineData(15, 120, 60, 24, 0, 24)]
    [InlineData(15, 120, 60, 24, 73, 24)]
    [InlineData(15, 120, 60, 24, 24, 0)]
    public async Task Booking_rules_outside_the_allowed_ranges_are_rejected(
        int interval, int lead, int horizon, int cancel, int expiry, int retention)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var response = await owner.PutAsync(Rules, new BookingRules(interval, lead, horizon, cancel, false, expiry, retention));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("settings.", (await response.ProblemCodeAsync())!);
    }

    [Fact]
    public async Task A_slug_suggestion_is_unique_across_salons()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("a@example.com", "Haarwerk");

        var free = await owner.GetJsonAsync<SlugSuggestion>("/api/settings/slug-suggestion?name=Kamm%20%26%20Schere");
        var taken = await owner.GetJsonAsync<SlugSuggestion>("/api/settings/slug-suggestion?name=Haarwerk");
        var empty = await owner.GetAsync("/api/settings/slug-suggestion?name=%20");

        Assert.Equal("kamm-schere", free.Slug);
        Assert.Equal("haarwerk-2", taken.Slug);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }
}
