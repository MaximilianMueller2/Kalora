using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class TimestampTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Save_sets_created_and_updated_timestamps_from_the_time_provider()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero));
        var salon = new Salon { Name = "Clocked", Slug = $"clocked-{Guid.NewGuid():N}" };

        await using (var db = TestDb.CreateContext(connectionString, clock))
        {
            db.Salons.Add(salon);
            await db.SaveChangesAsync();
        }

        clock.Advance(TimeSpan.FromHours(2));
        await using (var db = TestDb.CreateContext(connectionString, clock))
        {
            var loaded = await db.Salons.SingleAsync(s => s.Id == salon.Id);
            loaded.Name = "Renamed";
            await db.SaveChangesAsync();
        }

        await using var verify = TestDb.CreateContext(connectionString, clock);
        var stored = await verify.Salons.AsNoTracking().SingleAsync(s => s.Id == salon.Id);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero), stored.CreatedAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero), stored.UpdatedAtUtc);
    }
}
