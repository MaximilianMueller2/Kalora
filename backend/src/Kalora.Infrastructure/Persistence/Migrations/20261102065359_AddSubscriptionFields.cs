using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "admin_note",
                table: "salons",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trial_day_reminder_sent_at_utc",
                table: "salons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trial_week_reminder_sent_at_utc",
                table: "salons",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "admin_note",
                table: "salons");

            migrationBuilder.DropColumn(
                name: "trial_day_reminder_sent_at_utc",
                table: "salons");

            migrationBuilder.DropColumn(
                name: "trial_week_reminder_sent_at_utc",
                table: "salons");
        }
    }
}
