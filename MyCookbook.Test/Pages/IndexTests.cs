using Bunit;
using MyCookbook.Components;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Pages
{
    public class IndexTests : BlazorTestBase
    {
        public IndexTests(TestingWebAppFactory<Program> factory) : base(factory)
        {
        }

        [Fact]
        public async Task IndexPage_WhenRendered_DisplaysHeadersAndTables()
        {
            var testUser = "test-user";

            var dbService = GetDbService();
            await dbService.CreateRecipeAsync(new Recipe { Name = "Favorite Pancake", UserName = testUser }, testUser);

            var longestUncookedRecipes = new List<Recipe>();
            for (int i = 0; i < 12; i++)
            {
                longestUncookedRecipes.Add(new Recipe
                {
                    Name = $"Old Recipe {i}",
                    UserName = testUser,
                    LastCooked = DateTime.UtcNow.AddDays(-20 + i)
                });
            }
            foreach (var recipe in longestUncookedRecipes)
            {
                await dbService.CreateRecipeAsync(recipe, testUser);
            }

            var cut = RenderComponent<MyCookbook.Pages.Index>();

            Assert.Contains("<h1>Welcome, test-user!</h1>", cut.Markup);
            Assert.Contains("Favorites", cut.Markup);
            Assert.Contains("Long uncooked", cut.Markup);

            Assert.Contains("Favorite Pancake", cut.Markup);

            for (int i = 0; i < 8; i++)
            {
                Assert.Contains($"Old Recipe {i}", cut.Markup);
            }
            Assert.DoesNotContain("Old Recipe 10", cut.Markup);
            Assert.DoesNotContain("Old Recipe 11", cut.Markup);
        }

        [Fact]
        public async void IndexPage_WhenUserIsNotAuthenticated_RendersWithoutData()
        {
            var cut = RenderComponent<MyCookbook.Pages.Index>();

            Assert.Contains("<h1>Welcome, test-user!</h1>", cut.Markup);

            var recipeTables = cut.FindComponents<RecipeTable>();
            Assert.Equal(2, recipeTables.Count);

            foreach (var table in recipeTables)
            {
                Assert.Empty(table.Instance.Recipes);
            }
        }
    }
}