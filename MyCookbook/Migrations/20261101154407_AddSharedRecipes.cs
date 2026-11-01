using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyCookbook.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserName = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    OwnerUserName = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RecipeGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    LastOpened = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Pinned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharedItems_UserName",
                table: "SharedItems",
                column: "UserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedItems");
        }
    }
}
