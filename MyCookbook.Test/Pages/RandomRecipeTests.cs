using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Pages;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Pages
{
    public class RandomRecipeTests : BlazorTestBase
    {
        public RandomRecipeTests(TestingWebAppFactory<Program> factory) : base(factory)
        {
        }

        [Fact]
        public async Task RandomPage_WhenUserHasRecipes_NavigatesToARandomRecipe()
        {
            var testUser = "test-user";

            var dbService = GetDbService();
            var recipe1 = await dbService.CreateRecipeAsync(new Recipe { Name = "Random Pancake", UserName = testUser }, testUser);
            var recipe2 = await dbService.CreateRecipeAsync(new Recipe { Name = "Random Taco", UserName = testUser }, testUser);

            var navManager = Services.GetRequiredService<FakeNavigationManager>();

            RenderComponent<RandomRecipe>();

            var navigatedToUri = navManager.Uri.Replace(navManager.BaseUri, "");
            var possibleUris = new[] { $"Recipe/{recipe1.Name}", $"Recipe/{recipe2.Name}" };

            Assert.Contains(navigatedToUri, possibleUris);
        }

        [Fact]
        public async Task RandomPage_WhenUserHasNoRecipes_DoesNotNavigate()
        {
            var navManager = Services.GetRequiredService<FakeNavigationManager>();
            var initialUri = navManager.Uri;

            RenderComponent<RandomRecipe>();

            Assert.Equal(initialUri, navManager.Uri);
        }
    }
}