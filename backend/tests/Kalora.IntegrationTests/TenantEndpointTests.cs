using System.Net;
using System.Net.Http.Json;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class TenantEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_signed_in_salon_lists_only_its_own_data()
    {
        var (world, factory) = await StartAsync();
        using var client = factory.CreateClientFor(world.SalonA.Id);

        var names = await client.GetFromJsonAsync<string[]>("/api/test/staff");

        Assert.Equal(["Anna"], names!);
    }

    [Fact]
    public async Task A_foreign_id_in_the_url_behaves_like_a_missing_record()
    {
        var (world, factory) = await StartAsync();
        using var client = factory.CreateClientFor(world.SalonA.Id);
        var foreignUrl = $"/api/test/staff/{world.StaffB.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(foreignUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(foreignUrl, new RenameRequest("Hijacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(foreignUrl)).StatusCode);

        await AssertStaffBUntouchedAsync(world);
    }

    [Fact]
    public async Task Own_records_can_be_read_changed_and_deleted()
    {
        var (world, factory) = await StartAsync();
        using var client = factory.CreateClientFor(world.SalonA.Id);
        var ownUrl = $"/api/test/staff/{world.StaffA.Id}";

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ownUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(ownUrl, new RenameRequest("Anna K."))).StatusCode);
        Assert.Equal("Anna K.", await client.GetStringAsync(ownUrl));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(ownUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ownUrl)).StatusCode);
    }

    [Fact]
    public async Task A_salon_id_in_the_request_body_cannot_target_another_salon()
    {
        var (world, factory) = await StartAsync();
        using var client = factory.CreateClientFor(world.SalonA.Id);

        var response = await client.PostAsJsonAsync("/api/test/staff", new CreateStaffRequest("Intruder", world.SalonB.Id));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertStaffBUntouchedAsync(world);
    }

    [Fact]
    public async Task Created_records_belong_to_the_signed_in_salon()
    {
        var (world, factory) = await StartAsync();
        using var client = factory.CreateClientFor(world.SalonA.Id);

        var response = await client.PostAsJsonAsync("/api/test/staff", new CreateStaffRequest("Carla", null));
        var id = await response.Content.ReadFromJsonAsync<Guid>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = TestDb.CreateContext(world.ConnectionString);
        Assert.Equal(world.SalonA.Id, (await db.AllTenants<StaffMember>().SingleAsync(s => s.Id == id)).SalonId);
    }

    [Fact]
    public async Task Anonymous_requests_see_nothing()
    {
        var (world, factory) = await StartAsync();
        using var client = factory.CreateClientFor(null);

        Assert.Empty((await client.GetFromJsonAsync<string[]>("/api/test/staff"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/test/staff/{world.StaffA.Id}")).StatusCode);
    }

    [Fact]
    public async Task Public_endpoints_use_the_salon_from_the_slug()
    {
        var (world, factory) = await StartAsync();
        using var anonymous = factory.CreateClientFor(null);

        var names = await anonymous.GetFromJsonAsync<string[]>($"/api/public/salons/{world.SalonB.Slug}/test-staff");

        Assert.Equal(["Bea"], names!);
    }

    [Fact]
    public async Task Public_endpoints_ignore_the_signed_in_salon()
    {
        var (world, factory) = await StartAsync();
        using var signedInAsA = factory.CreateClientFor(world.SalonA.Id);

        var names = await signedInAsA.GetFromJsonAsync<string[]>($"/api/public/salons/{world.SalonB.Slug}/test-staff");

        Assert.Equal(["Bea"], names!);
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("INVALID_slug!")]
    public async Task Public_endpoints_with_an_unknown_slug_return_not_found_even_when_signed_in(string slug)
    {
        var (world, factory) = await StartAsync();
        using var signedInAsA = factory.CreateClientFor(world.SalonA.Id);

        var response = await signedInAsA.GetAsync($"/api/public/salons/{slug}/test-staff");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("salon.not_found", await response.Content.ReadAsStringAsync());
    }

    private async Task<(TwoSalons World, TenantApiFactory Factory)> StartAsync()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        return (world, new TenantApiFactory(world.ConnectionString));
    }

    private static async Task AssertStaffBUntouchedAsync(TwoSalons world)
    {
        await using var db = TestDb.CreateContext(world.ConnectionString);
        var all = await db.AllTenants<StaffMember>().Where(s => s.SalonId == world.SalonB.Id).ToListAsync();
        var staffB = Assert.Single(all);
        Assert.Equal("Bea", staffB.DisplayName);
    }
}
