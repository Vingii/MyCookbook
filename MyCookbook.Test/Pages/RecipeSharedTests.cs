using Bunit;
using MudBlazor;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Pages;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Pages
{
    public class RecipeSharedTests : BlazorTestBase
    {
        public RecipeSharedTests(TestingWebAppFactory<Program> factory) : base(factory)
        {
        }

        private async Task<(Recipe recipe, string testUser)> SetupTestRecipe(string owner, bool isFavorite = false)
        {
            var dbService = GetDbService();
            var testUser = "test-user";

            var recipe = new Recipe
            {
                Guid = Guid.NewGuid(),
                Name = "Shared Pizza",
                UserName = owner,
                Servings = 4,
                Ingredients = new List<Ingredient> { new Ingredient { Name = "Dough", Amount = "1 ball", Order = 1, UserName = owner } },
                Steps = new List<Step> { new Step { Description = "Put it in the oven.", Order = 1, UserName = owner } }
            };
            var createdRecipe = await dbService.CreateRecipeAsync(recipe, owner);

            if (isFavorite)
            {
                await dbService.AddFavoriteAsync(createdRecipe, testUser);
            }

            return (createdRecipe, testUser);
        }

        [Fact]
        public async Task RecipeShared_WhenRendered_DisplaysRecipeDetails()
        {
            var (recipe, testUser) = await SetupTestRecipe("owner-user");

            var cut = RenderComponent<RecipeShared>(parameters => parameters
                .Add(p => p.Id, recipe.Guid.ToString()));

            Assert.Contains(recipe.Name, cut.Markup);
            Assert.Contains($"Servings: {recipe.Servings}", cut.Markup);
            Assert.Contains("Dough", cut.Markup);
            Assert.Contains("1 ball", cut.Markup);
            Assert.Contains("1. Put it in the oven.", cut.Markup);
        }

        [Fact]
        public async Task ToggleFavorite_WhenNotFavorited_AddsAndUpdatesIcon()
        {
            var (recipe, testUser) = await SetupTestRecipe("owner-user", isFavorite: false);
            var dbService = GetDbService();

            var cut = RenderComponent<RecipeShared>(parameters => parameters
                .Add(p => p.Id, recipe.Guid.ToString()));

            Assert.Contains(Icons.Material.Outlined.StarBorder, cut.Markup);
            Assert.DoesNotContain(Icons.Material.Outlined.Star, cut.Markup);
            Assert.Empty(await dbService.GetFavoriteRecipesAsync(testUser));

            cut.Find(".mud-icon-root.cursor-pointer").Click();

            Assert.Contains(Icons.Material.Outlined.Star, cut.Markup);
            Assert.DoesNotContain(Icons.Material.Outlined.StarBorder, cut.Markup);
            Assert.Single(await dbService.GetFavoriteRecipesAsync(testUser));
        }

        [Fact]
        public async Task ToggleFavorite_WhenFavorited_RemovesAndUpdatesIcon()
        {
            var (recipe, testUser) = await SetupTestRecipe("owner-user", isFavorite: true);
            var dbService = GetDbService();

            var cut = RenderComponent<RecipeShared>(parameters => parameters
                .Add(p => p.Id, recipe.Guid.ToString()));

            Assert.Contains(Icons.Material.Outlined.Star, cut.Markup);
            Assert.Single(await dbService.GetFavoriteRecipesAsync(testUser));

            cut.Find(".mud-icon-root.cursor-pointer").Click();

            Assert.Contains(Icons.Material.Outlined.StarBorder, cut.Markup);
            Assert.Empty(await dbService.GetFavoriteRecipesAsync(testUser));
        }

        [Fact]
        public void RecipeShared_WhenIdIsInvalid_RendersWithoutCrashing()
        {
            var invalidGuid = Guid.NewGuid().ToString();

            var cut = RenderComponent<RecipeShared>(parameters => parameters
                .Add(p => p.Id, invalidGuid));

            Assert.Contains("h3", cut.Find("h3").ToMarkup());
            Assert.Contains("Servings", cut.Markup);
        }
    }
}