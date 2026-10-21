using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class InvitationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Accepting_an_invitation_signs_in_the_new_staff_member_with_the_staff_role()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        var staff = await host.AddStaffAsync(ownerMe.Salon!.Id, "Tom");
        var token = await InviteAsync(host, owner, staff.Id, "tom@example.com", SalonRole.Staff);
        var guest = host.NewSession();

        var preview = await guest.GetAsync($"/api/public/invitations/{token}");
        var accept = await guest.PostAsync($"/api/public/invitations/{token}/accept", new AcceptInvitationRequest(AuthTestHost.Password, "en"));

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var info = (await preview.Content.ReadFromJsonAsync<InvitationPreviewResponse>(TestJson.Options))!;
        Assert.Equal(("Haarwerk Müller", "Tom", "tom@example.com", SalonRole.Staff), (info.SalonName, info.StaffDisplayName, info.Email, info.Role));

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var me = await guest.MeAsync();
        Assert.Equal(SalonRole.Staff, me.Role);
        Assert.Equal(ownerMe.Salon.Id, me.Salon!.Id);
        Assert.Equal(staff.Id, me.StaffId);
        Assert.True(me.EmailConfirmed);
        Assert.Equal("en", me.PreferredLanguage);
    }

    [Fact]
    public async Task An_invitation_can_be_used_only_once()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        var staff = await host.AddStaffAsync(ownerMe.Salon!.Id, "Tom");
        var token = await InviteAsync(host, owner, staff.Id, "tom@example.com", SalonRole.Staff);
        await host.NewSession().PostAsync($"/api/public/invitations/{token}/accept", new AcceptInvitationRequest(AuthTestHost.Password, "de"));

        var again = await host.NewSession().PostAsync($"/api/public/invitations/{token}/accept", new AcceptInvitationRequest(AuthTestHost.Password, "de"));
        var preview = await host.NewSession().GetAsync($"/api/public/invitations/{token}");

        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal("invitation.invalid", await again.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
    }

    [Fact]
    public async Task An_invitation_expires_after_seven_days()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        var staff = await host.AddStaffAsync(ownerMe.Salon!.Id, "Tom");
        var token = await InviteAsync(host, owner, staff.Id, "tom@example.com", SalonRole.Staff);

        host.Clock.Advance(TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await host.NewSession().GetAsync($"/api/public/invitations/{token}")).StatusCode);

        host.Clock.Advance(TimeSpan.FromMinutes(2));
        var expired = await host.NewSession().PostAsync($"/api/public/invitations/{token}/accept", new AcceptInvitationRequest(AuthTestHost.Password, "de"));

        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        Assert.Equal("invitation.expired", await expired.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_garbage_token_is_not_found()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);

        var response = await host.NewSession().GetAsync("/api/public/invitations/not-a-real-token");

        Assert.Equal("invitation.invalid", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Only_a_hash_of_the_token_is_stored()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        var staff = await host.AddStaffAsync(ownerMe.Salon!.Id, "Tom");
        var token = await InviteAsync(host, owner, staff.Id, "tom@example.com", SalonRole.Staff);

        await using var db = TestDb.CreateContext(host.ConnectionString);
        var stored = await db.AllTenants<Kalora.Domain.Staff.StaffInvitation>().SingleAsync();

        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
    }

    [Fact]
    public async Task Staff_cannot_invite_and_anonymous_users_are_not_signed_in()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        var tom = await host.AddStaffAsync(ownerMe.Salon!.Id, "Tom");
        var lena = await host.AddStaffAsync(ownerMe.Salon.Id, "Lena");
        var staffSession = await AcceptAsync(host, owner, tom.Id, "tom@example.com", SalonRole.Staff);

        var asStaff = await staffSession.PostAsync("/api/invitations", new CreateInvitationRequest(lena.Id, "lena@example.com", SalonRole.Staff));
        var anonymous = await host.NewSession().PostAsync("/api/invitations", new CreateInvitationRequest(lena.Id, "lena@example.com", SalonRole.Staff));

        Assert.Equal(HttpStatusCode.Forbidden, asStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Managers_can_invite_staff_but_only_owners_can_invite_managers()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        var manager = await host.AddStaffAsync(ownerMe.Salon!.Id, "Mia");
        var lena = await host.AddStaffAsync(ownerMe.Salon.Id, "Lena");
        var ben = await host.AddStaffAsync(ownerMe.Salon.Id, "Ben");
        var managerSession = await AcceptAsync(host, owner, manager.Id, "mia@example.com", SalonRole.Manager);

        var staffInvite = await managerSession.PostAsync("/api/invitations", new CreateInvitationRequest(lena.Id, "lena@example.com", SalonRole.Staff));
        var managerInvite = await managerSession.PostAsync("/api/invitations", new CreateInvitationRequest(ben.Id, "ben@example.com", SalonRole.Manager));
        var ownerInvitesManager = await owner.PostAsync("/api/invitations", new CreateInvitationRequest(ben.Id, "ben@example.com", SalonRole.Manager));

        Assert.Equal(HttpStatusCode.OK, staffInvite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerInvite.StatusCode);
        Assert.Equal("invitation.manager_requires_owner", await managerInvite.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.OK, ownerInvitesManager.StatusCode);
    }

    [Fact]
    public async Task Nobody_can_be_invited_as_owner()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        var staff = await host.AddStaffAsync(ownerMe.Salon!.Id, "Tom");

        var response = await owner.PostAsync("/api/invitations", new CreateInvitationRequest(staff.Id, "tom@example.com", SalonRole.Owner));

        Assert.Equal("invitation.role_invalid", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_salon_cannot_invite_staff_of_another_salon()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (_, meA) = await host.RegisterOwnerAsync("a@example.com", "Salon A");
        var (ownerB, _) = await host.RegisterOwnerAsync("b@example.com", "Salon B");
        var staffOfA = await host.AddStaffAsync(meA.Salon!.Id, "Tom");

        var response = await ownerB.PostAsync("/api/invitations", new CreateInvitationRequest(staffOfA.Id, "tom@example.com", SalonRole.Staff));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("staff.not_found", await response.ProblemCodeAsync());
        Assert.Empty(await host.OutboxAsync(EmailTypes.StaffInvitation, "tom@example.com"));
    }

    [Fact]
    public async Task Accepting_with_an_email_that_already_has_an_account_is_a_conflict()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        await host.RegisterOwnerAsync("tom@example.com", "Toms eigener Salon");
        var staff = await host.AddStaffAsync(ownerMe.Salon!.Id, "Tom");
        var token = await InviteAsync(host, owner, staff.Id, "tom@example.com", SalonRole.Staff);

        var response = await host.NewSession().PostAsync($"/api/public/invitations/{token}/accept", new AcceptInvitationRequest(AuthTestHost.Password, "de"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth.email_taken", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_new_invitation_replaces_the_pending_one()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, ownerMe) = await host.RegisterOwnerAsync("maria@example.com");
        var staff = await host.AddStaffAsync(ownerMe.Salon!.Id, "Tom");
        var first = await InviteAsync(host, owner, staff.Id, "tom@example.com", SalonRole.Staff);
        var second = await InviteAsync(host, owner, staff.Id, "tom@example.com", SalonRole.Staff, expectedMails: 2);

        Assert.Equal(HttpStatusCode.NotFound, (await host.NewSession().GetAsync($"/api/public/invitations/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.NewSession().GetAsync($"/api/public/invitations/{second}")).StatusCode);
    }

    private static async Task<string> InviteAsync(
        AuthTestHost host, ApiSession owner, Guid staffId, string email, SalonRole role, int expectedMails = 1)
    {
        (await owner.PostAsync("/api/invitations", new CreateInvitationRequest(staffId, email, role))).EnsureSuccessStatusCode();
        var mails = await host.OutboxAsync(EmailTypes.StaffInvitation, email);
        Assert.Equal(expectedMails, mails.Count);
        var latest = mails.OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id).First();
        var url = System.Text.Json.JsonDocument.Parse(latest.PayloadJson!).RootElement.GetProperty("InvitationUrl").GetString()!;
        return Uri.UnescapeDataString(new Uri(url).Segments[^1]);
    }

    private static async Task<ApiSession> AcceptAsync(AuthTestHost host, ApiSession owner, Guid staffId, string email, SalonRole role)
    {
        var token = await InviteAsync(host, owner, staffId, email, role);
        var session = host.NewSession();
        (await session.PostAsync($"/api/public/invitations/{token}/accept", new AcceptInvitationRequest(AuthTestHost.Password, "de"))).EnsureSuccessStatusCode();
        return session;
    }
}
