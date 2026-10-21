using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_apply_to_an_empty_database_and_match_the_model()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = TestDb.CreateContext(connectionString);

        // Migrate() also throws if the model has changes that no migration covers.
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Schema_contains_all_tables_in_snake_case()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        var tables = await QueryStringsAsync(connectionString,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'");

        string[] expected =
        [
            "salons", "salon_opening_hours", "salon_closures", "salon_members", "staff_members",
            "staff_working_hours", "staff_absences", "service_categories", "services", "staff_services",
            "customers", "appointments", "time_blocks", "email_outbox", "audit_logs",
            "staff_invitations", "users", "user_claims", "user_logins", "user_tokens", "data_protection_keys",
        ];
        Assert.Equal(expected.Order(), tables.Order());
    }

    [Fact]
    public async Task Appointment_overlap_exclusion_constraint_exists()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        var constraints = await QueryStringsAsync(connectionString,
            "SELECT conname FROM pg_constraint WHERE contype = 'x'");

        Assert.Contains("ex_appointments_no_overlap", constraints);
    }

    private static async Task<List<string>> QueryStringsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}
