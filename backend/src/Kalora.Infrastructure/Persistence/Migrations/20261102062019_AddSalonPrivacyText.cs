using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalonPrivacyText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "privacy_text",
                table: "salons",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "privacy_text",
                table: "salons");
        }
    }
}
