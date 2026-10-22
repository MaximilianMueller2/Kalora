using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Salons;
using Kalora.Domain.Salons;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class OpeningHoursTests(PostgresFixture postgres)
{
    private const string Hours = "/api/settings/opening-hours";
    private const string Closures = "/api/settings/closures";

    private static OpeningBlock Block(string start, string end) => new(TimeOnly.Parse(start), TimeOnly.Parse(end));

    private static OpeningHoursPlan Plan(params (DayOfWeek Day, OpeningBlock[] Blocks)[] days) =>
        new(days.Select(d => new OpeningDay(d.Day, d.Blocks)).ToList());

    [Fact]
    public async Task A_new_salon_has_seven_closed_days_starting_on_monday()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var plan = await owner.GetJsonAsync<OpeningHoursPlan>(Hours);

        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            plan.Days.Select(d => d.Weekday));
        Assert.All(plan.Days, day => Assert.Empty(day.Blocks));
    }

    [Fact]
    public async Task The_week_can_be_replaced_with_several_blocks_per_day_and_closed_days()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var response = await owner.PutAsync(Hours, Plan(
            (DayOfWeek.Tuesday, [Block("09:00", "13:00"), Block("14:00", "18:00")]),
            (DayOfWeek.Saturday, [Block("09:00", "14:00")])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await owner.GetJsonAsync<OpeningHoursPlan>(Hours);
        Assert.Equal([Block("09:00", "13:00"), Block("14:00", "18:00")], stored.Days.Single(d => d.Weekday == DayOfWeek.Tuesday).Blocks);
        Assert.Equal([Block("09:00", "14:00")], stored.Days.Single(d => d.Weekday == DayOfWeek.Saturday).Blocks);
        Assert.Empty(stored.Days.Single(d => d.Weekday == DayOfWeek.Monday).Blocks);
    }

    [Fact]
    public async Task Times_travel_as_hours_and_minutes_and_a_replacement_removes_what_is_no_longer_listed()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("09:00", "18:00")]), (DayOfWeek.Friday, [Block("10:00", "16:00")])));

        await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("08:30", "12:00")])));

        var raw = await (await owner.GetAsync(Hours)).Content.ReadAsStringAsync();
        Assert.Contains("\"start\":\"08:30\"", raw);
        var stored = await owner.GetJsonAsync<OpeningHoursPlan>(Hours);
        Assert.Equal([Block("08:30", "12:00")], stored.Days.Single(d => d.Weekday == DayOfWeek.Monday).Blocks);
        Assert.Empty(stored.Days.Single(d => d.Weekday == DayOfWeek.Friday).Blocks);
    }

    [Fact]
    public async Task Blocks_may_touch_but_not_overlap()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var touching = await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("13:00", "18:00"), Block("09:00", "13:00")])));
        var overlapping = await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("09:00", "13:01"), Block("13:00", "18:00")])));

        Assert.Equal(HttpStatusCode.OK, touching.StatusCode);
        Assert.Equal("opening_hours.overlap", await overlapping.ProblemCodeAsync());
    }

    [Fact]
    public async Task Invalid_plans_are_rejected_and_leave_the_stored_plan_untouched()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("09:00", "18:00")])));

        var endBeforeStart = await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("18:00", "09:00")])));
        var zeroLength = await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("09:00", "09:00")])));
        var tooMany = await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("06:00", "07:00"), Block("08:00", "09:00"), Block("10:00", "11:00"), Block("12:00", "13:00"), Block("14:00", "15:00")])));
        var duplicateDay = await owner.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("09:00", "10:00")]), (DayOfWeek.Monday, [Block("11:00", "12:00")])));

        Assert.Equal("opening_hours.block_invalid", await endBeforeStart.ProblemCodeAsync());
        Assert.Equal("opening_hours.block_invalid", await zeroLength.ProblemCodeAsync());
        Assert.Equal("opening_hours.too_many_blocks", await tooMany.ProblemCodeAsync());
        Assert.Equal("opening_hours.duplicate_day", await duplicateDay.ProblemCodeAsync());
        var stored = await owner.GetJsonAsync<OpeningHoursPlan>(Hours);
        Assert.Equal([Block("09:00", "18:00")], stored.Days.Single(d => d.Weekday == DayOfWeek.Monday).Blocks);
    }

    [Fact]
    public async Task A_malformed_time_is_a_bad_request()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        await owner.RefreshAntiforgeryAsync();

        var response = await owner.Client.PutAsJsonAsync(Hours, new { days = new[] { new { weekday = "Monday", blocks = new[] { new { start = "25:99", end = "18:00" } } } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Each_salon_has_its_own_opening_hours_and_staff_cannot_change_them()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (ownerA, meA) = await host.RegisterOwnerAsync("a@example.com", "Salon A");
        var (ownerB, _) = await host.RegisterOwnerAsync("b@example.com", "Salon B");
        var staff = await host.InviteAndAcceptAsync(ownerA, meA.Salon!.Id, "Tom", "tom@example.com", SalonRole.Staff);

        await ownerA.PutAsync(Hours, Plan((DayOfWeek.Monday, [Block("09:00", "18:00")])));

        Assert.All((await ownerB.GetJsonAsync<OpeningHoursPlan>(Hours)).Days, day => Assert.Empty(day.Blocks));
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsync(Hours, Plan())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(Closures)).StatusCode);
    }

    [Fact]
    public async Task A_single_day_or_a_range_of_days_can_be_closed_and_listed_in_order()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var holiday = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 24), null, " Heiligabend "));
        var vacation = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 28), new DateOnly(2026, 12, 30), "Betriebsurlaub"));

        Assert.Equal(HttpStatusCode.OK, holiday.StatusCode);
        Assert.Equal(3, (await vacation.Content.ReadFromJsonAsync<List<Closure>>(TestJson.Options))!.Count);
        var listed = await owner.GetJsonAsync<List<Closure>>(Closures);
        Assert.Equal(
            [new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 28), new DateOnly(2026, 12, 29), new DateOnly(2026, 12, 30)],
            listed.Select(c => c.Date));
        Assert.Equal("Heiligabend", listed[0].Reason);
    }

    [Fact]
    public async Task Days_that_are_already_closed_are_skipped_and_a_fully_duplicate_request_is_a_conflict()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 29), null, null));

        var overlapping = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 28), new DateOnly(2026, 12, 30), null));
        var duplicate = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 29), null, null));

        Assert.Equal(2, (await overlapping.Content.ReadFromJsonAsync<List<Closure>>(TestJson.Options))!.Count);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("closure.already_exists", await duplicate.ProblemCodeAsync());
        Assert.Equal(3, (await owner.GetJsonAsync<List<Closure>>(Closures)).Count);
    }

    [Fact]
    public async Task Closures_are_validated()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var past = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 11, 2), null, null));
        var reversed = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 10), new DateOnly(2026, 12, 9), null));
        var tooLong = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 1), new DateOnly(2027, 2, 15), null));
        var longReason = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 1), null, new string('x', 501)));

        Assert.Equal("closure.date_in_past", await past.ProblemCodeAsync());
        Assert.Equal("closure.range_invalid", await reversed.ProblemCodeAsync());
        Assert.Equal("closure.range_too_long", await tooLong.ProblemCodeAsync());
        Assert.Equal("validation.too_long", await longReason.ProblemCodeAsync());
    }

    [Fact]
    public async Task Today_means_the_salons_local_day_and_past_closures_drop_out_of_the_list()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 11, 4), null, null));

        // 3 Nov 2026 23:30 UTC is already 4 Nov 00:30 in Berlin: 3 Nov is over, 4 Nov is today.
        host.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 3, 23, 30, 0, TimeSpan.Zero));
        var yesterday = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 11, 3), null, null));
        var today = await owner.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 11, 4), null, null));
        Assert.Equal("closure.date_in_past", await yesterday.ProblemCodeAsync());
        Assert.Equal("closure.already_exists", await today.ProblemCodeAsync());
        Assert.Single(await owner.GetJsonAsync<List<Closure>>(Closures));

        host.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 4, 23, 30, 0, TimeSpan.Zero));
        Assert.Empty(await owner.GetJsonAsync<List<Closure>>(Closures));
    }

    [Fact]
    public async Task A_closure_can_be_deleted_but_only_by_its_own_salon()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (ownerA, _) = await host.RegisterOwnerAsync("a@example.com", "Salon A");
        var (ownerB, _) = await host.RegisterOwnerAsync("b@example.com", "Salon B");
        var created = await (await ownerA.PostAsync(Closures, new CreateClosureRequest(new DateOnly(2026, 12, 24), null, null)))
            .Content.ReadFromJsonAsync<List<Closure>>(TestJson.Options);
        var id = created![0].Id;

        var foreign = await ownerB.DeleteAsync($"{Closures}/{id}");
        var own = await ownerA.DeleteAsync($"{Closures}/{id}");
        var again = await ownerA.DeleteAsync($"{Closures}/{id}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
        Assert.Equal("closure.not_found", await again.ProblemCodeAsync());
        Assert.Empty(await ownerA.GetJsonAsync<List<Closure>>(Closures));
    }
}
