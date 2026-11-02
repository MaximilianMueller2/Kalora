using System.Net;
using Kalora.Application.Onboarding;
using Kalora.Domain.Salons;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class OnboardingTests(PostgresFixture postgres)
{
    private static async Task<OnboardingStatusDto> StatusAsync(ApiSession session) => await session.GetJsonAsync<OnboardingStatusDto>("/api/onboarding");

    private static string[] Done(OnboardingStatusDto status) => status.Steps.Where(s => s.Done).Select(s => s.Key).ToArray();

    [Fact]
    public async Task A_new_salon_has_nothing_done_and_the_steps_come_in_the_order_of_the_assistant()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");

        var status = await StatusAsync(owner);

        Assert.Equal(["profile", "hours", "services", "team"], status.Steps.Select(s => s.Key).ToArray());
        Assert.Empty(Done(status));
        Assert.False(status.Complete);
        Assert.EndsWith($"/{me.Salon!.Slug}", status.BookingUrl);
    }

    [Fact]
    public async Task Each_step_counts_when_its_data_exists_and_the_salon_is_complete_when_all_do()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);

        await salon.ConfigureSalonAsync(s =>
        {
            s.Street = "Hauptstraße 1";
            s.PostalCode = "10115";
            s.City = "Berlin";
            s.Phone = "+49 30 123456";
        });
        Assert.Equal(["profile"], Done(await StatusAsync(owner)));

        await salon.OpenAsync();
        Assert.Equal(["profile", "hours"], Done(await StatusAsync(owner)));

        var cut = await salon.AddServiceAsync();
        Assert.Equal(["profile", "hours", "services"], Done(await StatusAsync(owner)));

        await salon.AddStaffAsync("Anna", 0, offers: cut);
        var complete = await StatusAsync(owner);
        Assert.Equal(["profile", "hours", "services", "team"], Done(complete));
        Assert.True(complete.Complete);
    }

    [Fact]
    public async Task The_address_needs_street_postal_code_city_and_phone()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);

        foreach (var missing in new[] { "street", "postal", "city", "phone" })
        {
            await salon.ConfigureSalonAsync(s =>
            {
                s.Street = missing == "street" ? string.Empty : "Hauptstraße 1";
                s.PostalCode = missing == "postal" ? string.Empty : "10115";
                s.City = missing == "city" ? string.Empty : "Berlin";
                s.Phone = missing == "phone" ? string.Empty : "+49 30 123456";
            });

            Assert.DoesNotContain("profile", Done(await StatusAsync(owner)));
        }
    }

    [Fact]
    public async Task Someone_nobody_can_book_is_not_a_team_yet()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        var hidden = await salon.AddServiceAsync("Intern", online: false);
        var inactive = await salon.AddServiceAsync("Alt", active: false);
        var cut = await salon.AddServiceAsync("Schnitt");

        await salon.AddStaffAsync("Ohne Leistung", 0);
        await salon.AddStaffAsync("Nur intern", 1, offers: [hidden, inactive]);
        await salon.AddStaffAsync("Inaktiv", 2, active: false, offers: cut);
        await salon.AddStaffAsync("Nicht online", 3, online: false, offers: cut);
        Assert.DoesNotContain("team", Done(await StatusAsync(owner)));

        await salon.AddStaffAsync("Anna", 4, offers: cut);
        Assert.Contains("team", Done(await StatusAsync(owner)));
    }

    [Fact]
    public async Task A_person_without_working_hours_cannot_be_booked_and_does_not_count()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var salon = new SalonSeeder(host.ConnectionString, me.Salon!.Id);
        var cut = await salon.AddServiceAsync();
        var anna = await salon.AddStaffAsync("Anna", 0, offers: cut);
        await salon.WithDbAsync(async db =>
        {
            db.StaffWorkingHours.RemoveRange(db.StaffWorkingHours.Where(h => h.StaffId == anna.Id));
            await db.SaveChangesAsync();
            return 0;
        });

        Assert.DoesNotContain("team", Done(await StatusAsync(owner)));
    }

    [Fact]
    public async Task Only_the_data_of_the_own_salon_counts()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var (_, other) = await host.RegisterOwnerAsync("other@example.com", "Anderer Salon");
        var salon = new SalonSeeder(host.ConnectionString, other.Salon!.Id);
        await salon.OpenAsync();
        var cut = await salon.AddServiceAsync();
        await salon.AddStaffAsync("Anna", 0, offers: cut);

        Assert.Empty(Done(await StatusAsync(owner)));
    }

    [Fact]
    public async Task Staff_and_visitors_have_no_access()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var staff = await host.InviteAndAcceptAsync(owner, me.Salon!.Id, "Tom", "tom@example.com", SalonRole.Staff);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/onboarding")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewSession().GetAsync("/api/onboarding")).StatusCode);
    }

    [Fact]
    public async Task A_salon_that_can_only_read_can_still_see_its_progress()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        await new SalonSeeder(host.ConnectionString, me.Salon!.Id).ConfigureSalonAsync(s => s.Status = SalonStatus.Suspended);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/onboarding")).StatusCode);
    }
}
