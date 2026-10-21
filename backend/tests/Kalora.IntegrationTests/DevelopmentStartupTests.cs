using Kalora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class DevelopmentStartupTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Development_startup_applies_pending_migrations()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", connectionString);
        });

        using var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
