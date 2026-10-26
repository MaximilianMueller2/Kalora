using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Auth;
using Kalora.Application.Catalog;
using Kalora.Application.Common;
using Kalora.Application.Salons;
using Kalora.Application.Staff;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class StaffTests(PostgresFixture postgres)
{
    private const string Staff = "/api/staff";

    private static Kalora.Application.Staff.CreateStaffRequest NewStaff(string name = "Anna", string color = "#7c5cfc") => new(name, color, true, false, false);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private static async Task<StaffDto> AddStaffAsync(ApiSession session, string name = "Anna") =>
        await ReadAsync<StaffDto>(await session.PostAsync(Staff, NewStaff(name)));

    private static async Task<ServiceDto> AddServiceAsync(ApiSession owner, string name = "Haarschnitt", int minutes = 30)
    {
        var catalog = await ReadAsync<CatalogDto>(await owner.PostAsync("/api/catalog/categories", new SaveCategoryRequest($"Kat {name}")));
        var created = await ReadAsync<CatalogDto>(await owner.PostAsync("/api/catalog/services",
            new SaveServiceRequest(catalog.Categories[^1].Id, name, null, 3000, false, minutes, 0, 0, true, true)));
        return created.Categories.SelectMany(c => c.Services).Single(s => s.Name == name);
    }

    private static OpeningDay Day(DayOfWeek day, params (int From, int To)[] blocks) =>
        new(day, blocks.Select(b => new OpeningBlock(new TimeOnly(b.From, 0), new TimeOnly(b.To, 0))).ToList());

    private static SaveStaffScheduleRequest Plan(DateOnly validFrom, params OpeningDay[] days) => new(validFrom, days);

    private static readonly DateOnly Today = new(2026, 11, 3);

    // ---------------------------------------------------------------- members

    [Fact]
    public async Task A_manager_creates_staff_without_a_login_and_they_are_listed_in_order()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var anna = await AddStaffAsync(owner, "  Anna ");
        var ben = await AddStaffAsync(owner, "Ben");
        var list = await owner.GetJsonAsync<List<StaffDto>>(Staff);

        Assert.Equal(["Anna", "Ben"], list.Select(s => s.DisplayName));
        Assert.Equal([0, 1], list.Select(s => s.SortOrder));
        Assert.Equal(("#7C5CFC", true, false, true, false, null, false, false), (anna.Color, anna.IsBookableOnline, anna.AutoConfirm, anna.IsActive,
            anna.HasLogin, anna.Role, anna.IsMe, anna.InvitationPending));
        Assert.Equal(ben.Id, list[1].Id);
    }

    [Theory]
    [InlineData("", "#7C5CFC", "validation.required")]
    [InlineData("   ", "#7C5CFC", "validation.required")]
    [InlineData("Anna", "red", "staff.color_invalid")]
    [InlineData("Anna", "#12345", "staff.color_invalid")]
    [InlineData("Anna", "#GGGGGG", "staff.color_invalid")]
    public async Task Invalid_staff_is_rejected_with_a_translatable_code(string name, string color, string code)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var response = await owner.PostAsync(Staff, NewStaff(name, color));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task The_owner_can_take_a_calendar_column_for_themselves_only_once()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");

        var mine = await ReadAsync<StaffDto>(await owner.PostAsync(Staff, NewStaff("Maria") with { LinkToCurrentUser = true }));
        var again = await owner.PostAsync(Staff, NewStaff("Maria 2") with { LinkToCurrentUser = true });

        Assert.True(mine.IsMe);
        Assert.True(mine.HasLogin);
        Assert.Equal(SalonRole.Owner, mine.Role);
        Assert.Equal(mine.Id, (await owner.MeAsync()).StaffId);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("staff.already_linked", await again.ProblemCodeAsync());
        Assert.Null(me.StaffId);
    }

    [Fact]
    public async Task Staff_can_be_edited_and_deactivated_and_inactive_ones_are_listed_last()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner, "Anna");
        await AddStaffAsync(owner, "Ben");

        var updated = await ReadAsync<StaffDto>(await owner.PutAsync($"{Staff}/{anna.Id}", new SaveStaffRequest(" Anna K. ", "#00aa00", false, true, false)));
        var list = await owner.GetJsonAsync<List<StaffDto>>(Staff);

        Assert.Equal(("Anna K.", "#00AA00", false, true, false), (updated.DisplayName, updated.Color, updated.IsBookableOnline, updated.AutoConfirm, updated.IsActive));
        Assert.Equal(["Ben", "Anna K."], list.Select(s => s.DisplayName));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsync($"{Staff}/{Guid.NewGuid()}", new SaveStaffRequest("X", "#000000", true, false, true))).StatusCode);
        Assert.Equal("staff.not_found", await (await owner.GetAsync($"{Staff}/{Guid.NewGuid()}")).ProblemCodeAsync());
    }

    [Fact]
    public async Task Everyone_in_the_salon_can_read_the_team_but_only_managers_change_it()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner, "Anna");
        var tom = await host.InviteAndAcceptAsync(owner, me.Salon!.Id, "Tom", "tom@example.com", SalonRole.Staff);
        var mia = await host.InviteAndAcceptAsync(owner, me.Salon.Id, "Mia", "mia@example.com", SalonRole.Manager);

        var seenByTom = await tom.GetJsonAsync<List<StaffDto>>(Staff);

        Assert.Contains(seenByTom, s => s.DisplayName == "Tom" && s.IsMe && s.HasLogin && s.Role == SalonRole.Staff);
        Assert.Contains(seenByTom, s => s.DisplayName == "Mia" && !s.IsMe && s.Role == SalonRole.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.PostAsync(Staff, NewStaff())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.PutAsync($"{Staff}/{anna.Id}", new SaveStaffRequest("X", "#000000", true, false, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mia.PostAsync(Staff, NewStaff("Von Mia"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewSession().GetAsync(Staff)).StatusCode);
    }

    [Fact]
    public async Task An_invitation_marks_the_staff_member_as_pending_until_it_is_accepted()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner, "Anna");

        (await owner.PostAsync("/api/invitations", new CreateInvitationRequest(anna.Id, "anna@example.com", SalonRole.Staff))).EnsureSuccessStatusCode();
        var pending = await ReadAsync<StaffDto>(await owner.GetAsync($"{Staff}/{anna.Id}"));
        host.Clock.Advance(TimeSpan.FromDays(8));
        var expired = await ReadAsync<StaffDto>(await owner.GetAsync($"{Staff}/{anna.Id}"));

        Assert.True(pending.InvitationPending);
        Assert.False(pending.HasLogin);
        Assert.False(expired.InvitationPending);
        _ = me;
    }

    [Fact]
    public async Task One_salon_cannot_see_or_change_the_team_of_another()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (ownerA, _) = await host.RegisterOwnerAsync("a@example.com", "Salon A");
        var (ownerB, _) = await host.RegisterOwnerAsync("b@example.com", "Salon B");
        var annaA = await AddStaffAsync(ownerA, "Anna A");
        await AddStaffAsync(ownerB, "Ben B");

        Assert.Equal(["Ben B"], (await ownerB.GetJsonAsync<List<StaffDto>>(Staff)).Select(s => s.DisplayName));
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"{Staff}/{annaA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.PutAsync($"{Staff}/{annaA.Id}", new SaveStaffRequest("X", "#000000", true, false, true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"{Staff}/{annaA.Id}/services")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"{Staff}/{annaA.Id}/working-hours")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"{Staff}/{annaA.Id}/absences")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.PostAsync($"{Staff}/{annaA.Id}/absences", new CreateAbsenceRequest(Today, Today, AbsenceType.Vacation))).StatusCode);
    }

    // ---------------------------------------------------------------- skills

    [Fact]
    public async Task Skills_are_replaced_as_a_whole_with_optional_duration_overrides()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var cut = await AddServiceAsync(owner, "Schneiden");
        var color = await AddServiceAsync(owner, "Färben", 90);
        var wash = await AddServiceAsync(owner, "Waschen", 15);
        Assert.Empty((await owner.GetJsonAsync<StaffSkills>($"{Staff}/{anna.Id}/services")).Skills);

        var first = await ReadAsync<StaffSkills>(await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([new(cut.Id, null), new(color.Id, 120)])));
        var second = await ReadAsync<StaffSkills>(await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([new(color.Id, 100), new(wash.Id, null)])));
        var cleared = await ReadAsync<StaffSkills>(await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([])));

        Assert.Equivalent(new[] { (cut.Id, (int?)null), (color.Id, 120) }, first.Skills.Select(s => (s.ServiceId, s.DurationOverrideMinutes)));
        Assert.Equivalent(new[] { (color.Id, (int?)100), (wash.Id, null) }, second.Skills.Select(s => (s.ServiceId, s.DurationOverrideMinutes)));
        Assert.Empty(cleared.Skills);
    }

    [Fact]
    public async Task Invalid_skills_are_refused_and_nothing_is_changed()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var (otherOwner, _) = await host.RegisterOwnerAsync("b@example.com", "Salon B");
        var anna = await AddStaffAsync(owner);
        var cut = await AddServiceAsync(owner, "Schneiden");
        var foreign = await AddServiceAsync(otherOwner, "Fremd");
        await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([new(cut.Id, null)]));

        var duplicate = await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([new(cut.Id, null), new(cut.Id, 30)]));
        var badDuration = await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([new(cut.Id, 32)]));
        var unknown = await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([new(Guid.NewGuid(), null)]));
        var otherSalons = await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([new(foreign.Id, null)]));

        Assert.Equal("staff.skills_duplicate", await duplicate.ProblemCodeAsync());
        Assert.Equal("service.duration_invalid", await badDuration.ProblemCodeAsync());
        Assert.Equal("service.not_found", await unknown.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, otherSalons.StatusCode);
        Assert.Equal([cut.Id], (await owner.GetJsonAsync<StaffSkills>($"{Staff}/{anna.Id}/services")).Skills.Select(s => s.ServiceId));
    }

    [Fact]
    public async Task Deleting_a_service_removes_it_from_the_skills()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var cut = await AddServiceAsync(owner, "Schneiden");
        await owner.PutAsync($"{Staff}/{anna.Id}/services", new StaffSkills([new(cut.Id, null)]));

        (await owner.DeleteAsync($"/api/catalog/services/{cut.Id}")).EnsureSuccessStatusCode();

        Assert.Empty((await owner.GetJsonAsync<StaffSkills>($"{Staff}/{anna.Id}/services")).Skills);
    }

    // ---------------------------------------------------------------- working hours

    [Fact]
    public async Task A_new_staff_member_has_no_working_hours_and_a_plan_can_be_saved_from_today()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        Assert.Empty((await owner.GetJsonAsync<StaffSchedule>($"{Staff}/{anna.Id}/working-hours")).Versions);

        var saved = await ReadAsync<StaffSchedule>(await owner.PutAsync($"{Staff}/{anna.Id}/working-hours",
            Plan(Today, Day(DayOfWeek.Monday, (9, 13), (14, 18)), Day(DayOfWeek.Friday, (10, 16)))));

        var version = Assert.Single(saved.Versions);
        Assert.Equal((Today, (DateOnly?)null), (version.ValidFrom, version.ValidTo));
        Assert.Equal(7, version.Days.Count);
        Assert.Equal(DayOfWeek.Monday, version.Days[0].Weekday);
        Assert.Equal([new OpeningBlock(new TimeOnly(9, 0), new TimeOnly(13, 0)), new OpeningBlock(new TimeOnly(14, 0), new TimeOnly(18, 0))], version.Days[0].Blocks);
        Assert.Empty(version.Days[1].Blocks);
        Assert.Single(version.Days[4].Blocks);
    }

    [Fact]
    public async Task A_new_plan_replaces_the_old_one_from_its_start_date_and_the_old_one_ends_the_day_before()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var url = $"{Staff}/{anna.Id}/working-hours";
        await owner.PutAsync(url, Plan(Today, Day(DayOfWeek.Monday, (9, 18))));

        var from = Today.AddDays(14);
        var saved = await ReadAsync<StaffSchedule>(await owner.PutAsync(url, Plan(from, Day(DayOfWeek.Tuesday, (8, 12)))));

        Assert.Equal([(Today, (DateOnly?)from.AddDays(-1)), (from, null)], saved.Versions.Select(v => (v.ValidFrom, v.ValidTo)));
        Assert.Single(saved.Versions[0].Days[0].Blocks);
        Assert.Empty(saved.Versions[1].Days[0].Blocks);
        Assert.Single(saved.Versions[1].Days[1].Blocks);
    }

    [Fact]
    public async Task Saving_for_an_earlier_date_replaces_later_plans_and_the_same_date_overwrites()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var url = $"{Staff}/{anna.Id}/working-hours";
        await owner.PutAsync(url, Plan(Today, Day(DayOfWeek.Monday, (9, 18))));
        await owner.PutAsync(url, Plan(Today.AddDays(30), Day(DayOfWeek.Tuesday, (9, 18))));

        var replaced = await ReadAsync<StaffSchedule>(await owner.PutAsync(url, Plan(Today.AddDays(7), Day(DayOfWeek.Wednesday, (9, 12)))));
        var overwritten = await ReadAsync<StaffSchedule>(await owner.PutAsync(url, Plan(Today.AddDays(7), Day(DayOfWeek.Thursday, (9, 12)))));

        Assert.Equal([Today, Today.AddDays(7)], replaced.Versions.Select(v => v.ValidFrom));
        Assert.Equal([Today, Today.AddDays(7)], overwritten.Versions.Select(v => v.ValidFrom));
        Assert.Empty(overwritten.Versions[1].Days[2].Blocks);
        Assert.Single(overwritten.Versions[1].Days[3].Blocks);
    }

    [Fact]
    public async Task Plans_that_have_ended_are_no_longer_returned()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var url = $"{Staff}/{anna.Id}/working-hours";
        await owner.PutAsync(url, Plan(Today, Day(DayOfWeek.Monday, (9, 18))));
        await owner.PutAsync(url, Plan(Today.AddDays(7), Day(DayOfWeek.Tuesday, (9, 18))));

        host.Clock.Advance(TimeSpan.FromDays(7));
        var later = await owner.GetJsonAsync<StaffSchedule>(url);

        Assert.Equal([Today.AddDays(7)], later.Versions.Select(v => v.ValidFrom));
    }

    [Fact]
    public async Task Invalid_plans_are_rejected_without_changing_anything()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var url = $"{Staff}/{anna.Id}/working-hours";
        await owner.PutAsync(url, Plan(Today, Day(DayOfWeek.Monday, (9, 18))));

        var past = await owner.PutAsync(url, Plan(Today.AddDays(-1), Day(DayOfWeek.Monday, (9, 18))));
        var empty = await owner.PutAsync(url, Plan(Today.AddDays(1), Day(DayOfWeek.Monday)));
        var noDays = await owner.PutAsync(url, Plan(Today.AddDays(1)));
        var overlap = await owner.PutAsync(url, Plan(Today.AddDays(1), Day(DayOfWeek.Monday, (9, 13), (12, 18))));
        var backwards = await owner.PutAsync(url, Plan(Today.AddDays(1), Day(DayOfWeek.Monday, (13, 9))));
        var twice = await owner.PutAsync(url, Plan(Today.AddDays(1), Day(DayOfWeek.Monday, (9, 18)), Day(DayOfWeek.Monday, (9, 18))));

        Assert.Equal("staff.schedule_date_in_past", await past.ProblemCodeAsync());
        Assert.Equal("staff.schedule_empty", await empty.ProblemCodeAsync());
        Assert.Equal("staff.schedule_empty", await noDays.ProblemCodeAsync());
        Assert.Equal("opening_hours.overlap", await overlap.ProblemCodeAsync());
        Assert.Equal("opening_hours.block_invalid", await backwards.ProblemCodeAsync());
        Assert.Equal("opening_hours.duplicate_day", await twice.ProblemCodeAsync());
        var after = await owner.GetJsonAsync<StaffSchedule>(url);
        Assert.Equal((Today, (DateOnly?)null), (after.Versions.Single().ValidFrom, after.Versions.Single().ValidTo));
    }

    [Fact]
    public async Task Today_is_the_calendar_day_in_the_salons_time_zone()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var url = $"{Staff}/{anna.Id}/working-hours";
        // 23:30 UTC on 3 Nov is already 4 Nov in Berlin.
        host.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 3, 23, 30, 0, TimeSpan.Zero));

        var yesterday = await owner.PutAsync(url, Plan(Today, Day(DayOfWeek.Monday, (9, 18))));
        var today = await owner.PutAsync(url, Plan(Today.AddDays(1), Day(DayOfWeek.Monday, (9, 18))));

        Assert.Equal("staff.schedule_date_in_past", await yesterday.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, today.StatusCode);
    }

    [Fact]
    public async Task Staff_read_their_own_hours_but_not_those_of_colleagues_and_never_change_them()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner, "Anna");
        var tom = await host.InviteAndAcceptAsync(owner, me.Salon!.Id, "Tom", "tom@example.com", SalonRole.Staff);
        var tomId = (await tom.MeAsync()).StaffId!.Value;
        await owner.PutAsync($"{Staff}/{tomId}/working-hours", Plan(Today, Day(DayOfWeek.Monday, (9, 18))));

        Assert.Single((await tom.GetJsonAsync<StaffSchedule>($"{Staff}/{tomId}/working-hours")).Versions);
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.GetAsync($"{Staff}/{anna.Id}/working-hours")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.PutAsync($"{Staff}/{tomId}/working-hours", Plan(Today, Day(DayOfWeek.Monday, (0, 23))))).StatusCode);
    }

    // ---------------------------------------------------------------- absences

    [Fact]
    public async Task An_absence_covers_whole_days_in_the_salons_time_zone()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var service = await AddServiceAsync(owner);
        var nov4 = new DateOnly(2026, 11, 4);

        var created = await ReadAsync<AbsenceCreated>(await owner.PostAsync($"{Staff}/{anna.Id}/absences", new CreateAbsenceRequest(nov4, nov4.AddDays(1), AbsenceType.Vacation)));

        Assert.Equal((nov4, nov4.AddDays(1), AbsenceType.Vacation, 0), (created.Absence.StartDate, created.Absence.EndDate, created.Absence.Type, created.Absence.ConflictCount));
        Assert.Empty(created.AffectedAppointments);
        // Berlin is UTC+1 in November: 4 Nov 00:00 local is 3 Nov 23:00 UTC, and the range ends before 6 Nov 00:00 local.
        await using var db = TestDb.CreateContext(host.ConnectionString, salonId: me.Salon!.Id);
        var stored = await db.StaffAbsences.SingleAsync();
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 23, 0, 0, TimeSpan.Zero), stored.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 23, 0, 0, TimeSpan.Zero), stored.EndUtc);
        _ = service;
    }

    [Fact]
    public async Task Absences_can_be_listed_in_order_and_deleted_and_ended_ones_disappear()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var url = $"{Staff}/{anna.Id}/absences";
        var later = await ReadAsync<AbsenceCreated>(await owner.PostAsync(url, new CreateAbsenceRequest(Today.AddDays(20), Today.AddDays(22), AbsenceType.Private)));
        var sooner = await ReadAsync<AbsenceCreated>(await owner.PostAsync(url, new CreateAbsenceRequest(Today, Today, AbsenceType.Sick)));

        var list = await owner.GetJsonAsync<List<AbsenceDto>>(url);
        host.Clock.Advance(TimeSpan.FromDays(1));
        var afterOneDay = await owner.GetJsonAsync<List<AbsenceDto>>(url);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"{url}/{later.Absence.Id}")).StatusCode);
        var afterDelete = await owner.GetJsonAsync<List<AbsenceDto>>(url);
        var missing = await owner.DeleteAsync($"{url}/{Guid.NewGuid()}");

        Assert.Equal([sooner.Absence.Id, later.Absence.Id], list.Select(a => a.Id));
        Assert.Equal([later.Absence.Id], afterOneDay.Select(a => a.Id));
        Assert.Empty(afterDelete);
        Assert.Equal("absence.not_found", await missing.ProblemCodeAsync());
    }

    [Fact]
    public async Task Invalid_absences_are_rejected()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);
        var url = $"{Staff}/{anna.Id}/absences";

        var backwards = await owner.PostAsync(url, new CreateAbsenceRequest(Today.AddDays(2), Today.AddDays(1), AbsenceType.Vacation));
        var past = await owner.PostAsync(url, new CreateAbsenceRequest(Today.AddDays(-3), Today.AddDays(-1), AbsenceType.Vacation));
        var tooLong = await owner.PostAsync(url, new CreateAbsenceRequest(Today, Today.AddDays(366), AbsenceType.Vacation));
        await owner.RefreshAntiforgeryAsync();
        var badType = await owner.Client.PostAsJsonAsync(url, new { startDate = "2026-11-10", endDate = "2026-11-11", type = "Party" });

        Assert.Equal("absence.range_invalid", await backwards.ProblemCodeAsync());
        Assert.Equal("absence.date_in_past", await past.ProblemCodeAsync());
        Assert.Equal("absence.range_too_long", await tooLong.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        Assert.Empty(await owner.GetJsonAsync<List<AbsenceDto>>(url));
    }

    [Fact]
    public async Task An_absence_that_began_in_the_past_but_is_still_running_is_accepted()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner);

        var response = await owner.PostAsync($"{Staff}/{anna.Id}/absences", new CreateAbsenceRequest(Today.AddDays(-2), Today, AbsenceType.Sick));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Staff_manage_their_own_absences_and_managers_everyones()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner, "Anna");
        var tom = await host.InviteAndAcceptAsync(owner, me.Salon!.Id, "Tom", "tom@example.com", SalonRole.Staff);
        var tomId = (await tom.MeAsync()).StaffId!.Value;
        var request = new CreateAbsenceRequest(Today.AddDays(5), Today.AddDays(6), AbsenceType.Vacation);

        var own = await ReadAsync<AbsenceCreated>(await tom.PostAsync($"{Staff}/{tomId}/absences", request));
        var foreign = await tom.PostAsync($"{Staff}/{anna.Id}/absences", request);
        var byManager = await owner.PostAsync($"{Staff}/{tomId}/absences", request);
        var foreignList = await tom.GetAsync($"{Staff}/{anna.Id}/absences");

        Assert.Equal(2, (await tom.GetJsonAsync<List<AbsenceDto>>($"{Staff}/{tomId}/absences")).Count);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignList.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await tom.DeleteAsync($"{Staff}/{tomId}/absences/{own.Absence.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tom.DeleteAsync($"{Staff}/{anna.Id}/absences/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Open_and_confirmed_appointments_in_the_absence_are_returned_as_a_warning_but_the_absence_is_saved()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var salonId = me.Salon!.Id;
        var anna = await AddStaffAsync(owner, "Anna");
        var ben = await AddStaffAsync(owner, "Ben");
        var service = await AddServiceAsync(owner);
        var dayStart = new DateTimeOffset(2026, 11, 3, 23, 0, 0, TimeSpan.Zero);

        var confirmed = await host.AddBookingAsync(salonId, anna.Id, service.Id, dayStart.AddHours(10), AppointmentStatus.Confirmed);
        var requested = await host.AddBookingAsync(salonId, anna.Id, service.Id, dayStart.AddHours(12), AppointmentStatus.Requested);
        await host.AddBookingAsync(salonId, anna.Id, service.Id, dayStart.AddHours(14), AppointmentStatus.Declined);
        await host.AddBookingAsync(salonId, anna.Id, service.Id, dayStart.AddHours(15), AppointmentStatus.Cancelled);
        await host.AddBookingAsync(salonId, anna.Id, service.Id, dayStart.AddHours(-1), AppointmentStatus.Confirmed);
        await host.AddBookingAsync(salonId, anna.Id, service.Id, dayStart.AddHours(24), AppointmentStatus.Confirmed);
        await host.AddBookingAsync(salonId, ben.Id, service.Id, dayStart.AddHours(10), AppointmentStatus.Confirmed);
        var nov4 = new DateOnly(2026, 11, 4);

        var created = await ReadAsync<AbsenceCreated>(await owner.PostAsync($"{Staff}/{anna.Id}/absences", new CreateAbsenceRequest(nov4, nov4, AbsenceType.Sick)));

        Assert.Equal([confirmed, requested], created.AffectedAppointments.Select(a => a.Id));
        Assert.Equal([AppointmentStatus.Confirmed, AppointmentStatus.Requested], created.AffectedAppointments.Select(a => a.Status));
        Assert.All(created.AffectedAppointments, a => Assert.False(string.IsNullOrWhiteSpace(a.CustomerName)));
        Assert.Equal("Cut", created.AffectedAppointments[0].ServiceName);
        Assert.Equal(2, created.Absence.ConflictCount);
        var listed = Assert.Single(await owner.GetJsonAsync<List<AbsenceDto>>($"{Staff}/{anna.Id}/absences"));
        Assert.Equal(2, listed.ConflictCount);
    }

    [Fact]
    public async Task An_appointment_that_only_touches_the_edge_of_the_absence_is_no_conflict()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var anna = await AddStaffAsync(owner, "Anna");
        var ben = await AddStaffAsync(owner, "Ben");
        var service = await AddServiceAsync(owner);
        var nov4 = new DateOnly(2026, 11, 4);
        var absence = new CreateAbsenceRequest(nov4, nov4, AbsenceType.Private);
        // Absence on 4 Nov (Berlin) = [3 Nov 23:00Z, 4 Nov 23:00Z).
        await host.AddBookingAsync(me.Salon!.Id, anna.Id, service.Id, new DateTimeOffset(2026, 11, 3, 22, 30, 0, TimeSpan.Zero), AppointmentStatus.Confirmed);
        await host.AddBookingAsync(me.Salon.Id, anna.Id, service.Id, new DateTimeOffset(2026, 11, 4, 23, 0, 0, TimeSpan.Zero), AppointmentStatus.Confirmed);
        var overlapsStart = await host.AddBookingAsync(me.Salon.Id, ben.Id, service.Id, new DateTimeOffset(2026, 11, 3, 22, 45, 0, TimeSpan.Zero), AppointmentStatus.Confirmed);

        var touching = await ReadAsync<AbsenceCreated>(await owner.PostAsync($"{Staff}/{anna.Id}/absences", absence));
        var overlapping = await ReadAsync<AbsenceCreated>(await owner.PostAsync($"{Staff}/{ben.Id}/absences", absence));

        Assert.Empty(touching.AffectedAppointments);
        Assert.Equal([overlapsStart], overlapping.AffectedAppointments.Select(a => a.Id));
    }

    [Fact]
    public async Task Writing_requests_need_the_antiforgery_token()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var response = await owner.Client.PostAsJsonAsync(Staff, NewStaff());

        Assert.Equal("security.antiforgery_failed", await response.ProblemCodeAsync());
    }
}
