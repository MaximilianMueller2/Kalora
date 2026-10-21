using Kalora.Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kalora.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef`; generating migrations does not connect to the database.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("KALORA_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=kalora_design;Username=design;Password=design";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, new TenantContext());
    }
}
