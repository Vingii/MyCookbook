using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCookbook.Migrations
{
    /// <inheritdoc />
    public partial class AddStepsUserNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Steps_UserName",
                table: "Steps",
                column: "UserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Steps_UserName",
                table: "Steps");
        }
    }
}
