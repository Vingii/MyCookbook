using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyCookbook.Migrations
{
    /// <inheritdoc />
    public partial class AddCookbooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CookbookId",
                table: "Recipes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cookbooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserName = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShowInPlanner = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cookbooks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CookbookId",
                table: "Recipes",
                column: "CookbookId");

            migrationBuilder.CreateIndex(
                name: "IX_Cookbooks_UserName",
                table: "Cookbooks",
                column: "UserName");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Cookbooks_CookbookId",
                table: "Recipes",
                column: "CookbookId",
                principalTable: "Cookbooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Cookbooks_CookbookId",
                table: "Recipes");

            migrationBuilder.DropTable(
                name: "Cookbooks");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_CookbookId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CookbookId",
                table: "Recipes");
        }
    }
}
