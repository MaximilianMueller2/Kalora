using Kalora.Application.Tenancy;
using Kalora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Kalora.IntegrationTests;

/// <summary>One real PostgreSQL 18 container shared by all database tests; each caller gets its own database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates an empty database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"kalora_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }

    /// <summary>Creates a migrated database and returns its connection string.</summary>
    public async Task<string> CreateMigratedDatabaseAsync()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = TestDb.CreateContext(connectionString);
        await db.Database.MigrateAsync();
        return connectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "database";
}

internal static class TestDb
{
    /// <summary>Creates a context acting for <paramref name="salonId"/>; without it the context has no tenant (system / pre-login).</summary>
    public static AppDbContext CreateContext(string connectionString, TimeProvider? timeProvider = null, Guid? salonId = null)
    {
        var tenant = new TenantContext();
        if (salonId is { } id)
        {
            tenant.Initialize(id);
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditTimestampInterceptor(timeProvider ?? TimeProvider.System))
            .Options;
        return new AppDbContext(options, tenant);
    }
}
