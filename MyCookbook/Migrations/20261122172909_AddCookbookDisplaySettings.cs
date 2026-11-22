using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCookbook.Migrations
{
    /// <inheritdoc />
    public partial class AddCookbookDisplaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing cookbooks keep showing everything (MC-81).
            migrationBuilder.AddColumn<bool>(
                name: "ShowDuration",
                table: "Cookbooks",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowLastCooked",
                table: "Cookbooks",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowServings",
                table: "Cookbooks",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowStepTimeline",
                table: "Cookbooks",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowDuration",
                table: "Cookbooks");

            migrationBuilder.DropColumn(
                name: "ShowLastCooked",
                table: "Cookbooks");

            migrationBuilder.DropColumn(
                name: "ShowServings",
                table: "Cookbooks");

            migrationBuilder.DropColumn(
                name: "ShowStepTimeline",
                table: "Cookbooks");
        }
    }
}
