using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCookbook.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedRecipeType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FromFridge",
                table: "PlannedRecipes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromFridge",
                table: "PlannedRecipes");
        }
    }
}
