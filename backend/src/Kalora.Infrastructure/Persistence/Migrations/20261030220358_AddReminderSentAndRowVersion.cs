using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSentAndRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reminder_sent_at_utc",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            // "xmin" is PostgreSQL's own row version column. The model maps it as the concurrency token, but it exists
            // in every table already and cannot be added.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reminder_sent_at_utc",
                table: "appointments");
        }
    }
}
