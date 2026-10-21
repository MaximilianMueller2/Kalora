using Kalora.Domain.Salons;
using Kalora.Domain.Staff;

namespace Kalora.IntegrationTests;

/// <summary>A migrated database holding two independent salons, each with one staff member.</summary>
internal sealed record TwoSalons(string ConnectionString, Salon SalonA, StaffMember StaffA, Salon SalonB, StaffMember StaffB)
{
    public static async Task<TwoSalons> CreateAsync(PostgresFixture postgres)
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var salonA = new Salon { Name = "Salon A", Slug = $"salon-a-{Guid.NewGuid():N}"[..30] };
        var salonB = new Salon { Name = "Salon B", Slug = $"salon-b-{Guid.NewGuid():N}"[..30] };
        var staffA = new StaffMember { SalonId = salonA.Id, DisplayName = "Anna" };
        var staffB = new StaffMember { SalonId = salonB.Id, DisplayName = "Bea" };

        await using var db = TestDb.CreateContext(connectionString);
        db.AddRange(salonA, salonB, staffA, staffB);
        await db.SaveChangesAsync();

        return new TwoSalons(connectionString, salonA, staffA, salonB, staffB);
    }
}
