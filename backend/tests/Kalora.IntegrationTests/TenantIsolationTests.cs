using Kalora.Domain.Common;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Kalora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class TenantIsolationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Queries_return_only_the_current_salons_rows()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);

        var staff = await db.StaffMembers.ToListAsync();
        var salons = await db.Salons.ToListAsync();

        Assert.Equal([world.StaffA.Id], staff.Select(s => s.Id));
        Assert.Equal([world.SalonA.Id], salons.Select(s => s.Id));
        Assert.Null(await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == world.StaffB.Id));
    }

    [Fact]
    public async Task Without_a_tenant_no_salon_data_is_visible()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString);

        Assert.Empty(await db.StaffMembers.ToListAsync());
        Assert.Empty(await db.Salons.ToListAsync());
    }

    [Fact]
    public async Task Bypass_methods_see_all_salons()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);

        Assert.Equal(2, await db.AllSalons().CountAsync(s => s.Id == world.SalonA.Id || s.Id == world.SalonB.Id));
        Assert.Equal(2, await db.AllTenants<StaffMember>().CountAsync(s => s.Id == world.StaffA.Id || s.Id == world.StaffB.Id));
    }

    [Fact]
    public async Task Every_salon_owned_entity_has_the_tenant_filter_and_a_salon_id_concurrency_token()
    {
        await using var db = TestDb.CreateContext("Host=unused");

        var salonOwned = db.Model.GetEntityTypes().Where(t => typeof(ISalonOwned).IsAssignableFrom(t.ClrType)).ToList();

        Assert.NotEmpty(salonOwned);
        foreach (var entityType in salonOwned)
        {
            Assert.Contains(entityType.GetDeclaredQueryFilters(), filter => filter.Key == "Tenant");
            Assert.True(entityType.FindProperty(nameof(ISalonOwned.SalonId))!.IsConcurrencyToken, entityType.ShortName());
        }

        Assert.Contains(db.Model.FindEntityType(typeof(Salon))!.GetDeclaredQueryFilters(), filter => filter.Key == "Tenant");
    }

    [Fact]
    public async Task Adding_an_entity_for_another_salon_is_rejected()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);
        db.StaffMembers.Add(new StaffMember { SalonId = world.SalonB.Id, DisplayName = "Intruder" });

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_new_entity_without_salon_id_gets_the_current_tenant()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        var created = new StaffMember { DisplayName = "Carla" };

        await using (var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id))
        {
            db.StaffMembers.Add(created);
            await db.SaveChangesAsync();
        }

        await using var verify = TestDb.CreateContext(world.ConnectionString);
        Assert.Equal(world.SalonA.Id, (await verify.AllTenants<StaffMember>().SingleAsync(s => s.Id == created.Id)).SalonId);
    }

    [Fact]
    public async Task Adding_without_salon_id_and_without_tenant_is_rejected()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString);
        db.StaffMembers.Add(new StaffMember { DisplayName = "Nobody's" });

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Moving_an_entity_to_another_salon_is_rejected()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);
        var own = await db.StaffMembers.SingleAsync();
        own.SalonId = world.SalonB.Id;

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
        await AssertStaffUnchangedAsync(world, world.StaffA);
    }

    [Fact]
    public async Task A_forged_update_with_a_foreign_id_is_rejected_and_changes_nothing()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);
        db.StaffMembers.Update(new StaffMember { Id = world.StaffB.Id, SalonId = world.SalonA.Id, DisplayName = "Hijacked" });

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
        await AssertStaffUnchangedAsync(world, world.StaffB);
    }

    [Fact]
    public async Task A_forged_delete_with_a_foreign_id_affects_no_row()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);
        db.StaffMembers.Remove(new StaffMember { Id = world.StaffB.Id, SalonId = world.SalonA.Id });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        await AssertStaffUnchangedAsync(world, world.StaffB);
    }

    [Fact]
    public async Task Changing_or_deleting_an_entity_loaded_through_the_bypass_is_rejected()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);
        var foreign = await db.AllTenants<StaffMember>().SingleAsync(s => s.Id == world.StaffB.Id);

        foreign.DisplayName = "Renamed by A";
        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        db.StaffMembers.Remove(await db.AllTenants<StaffMember>().SingleAsync(s => s.Id == world.StaffB.Id));
        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
        await AssertStaffUnchangedAsync(world, world.StaffB);
    }

    [Fact]
    public async Task A_salon_cannot_modify_another_salons_master_data()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);
        var other = await db.AllSalons().SingleAsync(s => s.Id == world.SalonB.Id);
        other.Name = "Taken over";

        await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Bulk_operations_respect_the_filter()
    {
        var world = await TwoSalons.CreateAsync(postgres);
        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonA.Id);

        var deleted = await db.StaffMembers.ExecuteDeleteAsync();

        Assert.Equal(1, deleted);
        await AssertStaffUnchangedAsync(world, world.StaffB);
    }

    private static async Task AssertStaffUnchangedAsync(TwoSalons world, StaffMember expected)
    {
        await using var db = TestDb.CreateContext(world.ConnectionString);
        var stored = await db.AllTenants<StaffMember>().SingleAsync(s => s.Id == expected.Id);
        Assert.Equal(expected.DisplayName, stored.DisplayName);
        Assert.Equal(expected.SalonId, stored.SalonId);
    }
}
