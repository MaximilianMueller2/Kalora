using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalonSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "imprint_text",
                table: "salons",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "privacy_policy_url",
                table: "salons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "request_expiry_hours",
                table: "salons",
                type: "integer",
                nullable: false,
                defaultValue: 24);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "imprint_text",
                table: "salons");

            migrationBuilder.DropColumn(
                name: "privacy_policy_url",
                table: "salons");

            migrationBuilder.DropColumn(
                name: "request_expiry_hours",
                table: "salons");
        }
    }
}
