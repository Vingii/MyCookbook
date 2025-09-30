using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCookbook.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDeleteBehaviorToCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteRecipes_Recipes_RecipeId",
                table: "FavoriteRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_PlannedRecipes_Recipes_RecipeId",
                table: "PlannedRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK__Steps__RecipeId__5BE2A6F2",
                table: "Steps");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Recipes_RecipeId",
                table: "Tags");

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteRecipes_Recipes_RecipeId",
                table: "FavoriteRecipes",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedRecipes_Recipes_RecipeId",
                table: "PlannedRecipes",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK__Steps__RecipeId__5BE2A6F2",
                table: "Steps",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Recipes_RecipeId",
                table: "Tags",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteRecipes_Recipes_RecipeId",
                table: "FavoriteRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_PlannedRecipes_Recipes_RecipeId",
                table: "PlannedRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK__Steps__RecipeId__5BE2A6F2",
                table: "Steps");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Recipes_RecipeId",
                table: "Tags");

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteRecipes_Recipes_RecipeId",
                table: "FavoriteRecipes",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedRecipes_Recipes_RecipeId",
                table: "PlannedRecipes",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Steps__RecipeId__5BE2A6F2",
                table: "Steps",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Recipes_RecipeId",
                table: "Tags",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id");
        }
    }
}
