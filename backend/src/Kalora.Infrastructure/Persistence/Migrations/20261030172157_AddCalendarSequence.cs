using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "calendar_sequence",
                table: "appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "calendar_sequence",
                table: "appointments");
        }
    }
}
