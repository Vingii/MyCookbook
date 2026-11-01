using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCookbook.Migrations
{
    /// <inheritdoc />
    public partial class UniqueRecipeGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Importing another user's export used to keep their recipe Guids. Give every duplicate a new Guid,
            // keeping it on the copy that the Shared page points to (else the oldest), and move the copies'
            // Shared page entries along with them.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "GuidFix" ON COMMIT DROP AS
                SELECT "Id", "Guid" AS "OldGuid", "UserName", gen_random_uuid() AS "NewGuid"
                FROM (
                    SELECT r."Id", r."Guid", r."UserName",
                           ROW_NUMBER() OVER (
                               PARTITION BY r."Guid"
                               ORDER BY CASE WHEN EXISTS (
                                            SELECT 1 FROM "SharedItems" s
                                            WHERE s."RecipeGuid" = r."Guid" AND s."OwnerUserName" = r."UserName")
                                        THEN 0 ELSE 1 END,
                                        r."Id") AS "Rank"
                    FROM "Recipes" r
                ) ranked
                WHERE "Rank" > 1;

                UPDATE "Recipes" r SET "Guid" = f."NewGuid" FROM "GuidFix" f WHERE r."Id" = f."Id";

                UPDATE "SharedItems" s SET "RecipeGuid" = f."NewGuid" FROM "GuidFix" f
                WHERE s."RecipeGuid" = f."OldGuid" AND s."OwnerUserName" = f."UserName";
                """);

            migrationBuilder.CreateIndex(
                name: "UQ_Recipes_Guid",
                table: "Recipes",
                column: "Guid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Recipes_Guid",
                table: "Recipes");
        }
    }
}
