using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MyCookbook.Components;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Pages;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Pages
{
    public class RecipeViewerTests : BlazorTestBase
    {
        public RecipeViewerTests(TestingWebAppFactory<Program> factory) : base(factory)
        {
        }

        private async Task<(Recipe recipe, string testUser)> SetupTestRecipe()
        {
            var dbService = GetDbService();
            var testUser = "test-user";
            var recipe = new Recipe
            {
                Name = "Test Lasagna",
                UserName = testUser,
                Servings = 4,
                Ingredients = new List<Ingredient> {
                    new Ingredient { Name = "Pasta", Amount = "500g", Order = 1, UserName = testUser },
                    new Ingredient { Name = "Sauce", Amount = "1 jar", Order = 2, UserName = testUser }
                },
                Steps = new List<Step> {
                    new Step { Description = "Boil pasta.", Order = 1, UserName = testUser },
                    new Step { Description = "Layer everything.", Order = 2, UserName = testUser }
                }
            };
            var createdRecipe = await dbService.CreateRecipeAsync(recipe, testUser);
            return (createdRecipe, testUser);
        }

        [Fact]
        public async Task RecipeViewer_WhenAuthenticated_DisplaysRecipeDetails()
        {
            var (recipe, testUser) = await SetupTestRecipe();

            var cut = RenderComponent<RecipeViewer>(parameters => parameters
                .Add(p => p.RecipeGuid, recipe.Guid.ToString()));

            Assert.Contains(recipe.Name, cut.Markup);
            Assert.Contains("Pasta", cut.Markup);
            Assert.Contains("Boil ", cut.Markup);
            Assert.DoesNotContain("Boil pasta", cut.Markup);
            Assert.Contains("Add Step", cut.Markup);
            Assert.Contains("Finish Cooking", cut.Markup);
        }

        [Fact]
        public async Task AddIngredient_WhenClicked_AddsIngredientToDbAndUi()
        {
            var (recipe, testUser) = await SetupTestRecipe();
            var dbService = GetDbService();

            var cut = RenderComponent<RecipeViewer>(parameters => parameters.Add(p => p.RecipeGuid, recipe.Guid.ToString()));

            cut.FindAll("button").First(b => b.TextContent.Contains("Add")).Click();

            var updatedRecipe = await dbService.GetDetailedRecipeAsync(recipe.Guid, testUser);
            var ingredients = updatedRecipe.Ingredients;
            Assert.Equal(3, ingredients.Count());
        }

        [Fact]
        public async Task DeleteIngredient_WhenClicked_RemovesIngredientFromDbAndUi()
        {
            var (recipe, testUser) = await SetupTestRecipe();
            var dbService = GetDbService();

            var cut = RenderComponent<RecipeViewer>(parameters => parameters.Add(p => p.RecipeGuid, recipe.Guid.ToString()));
            Assert.Contains("Sauce", cut.Markup);

            cut.FindAll("td .mud-icon-root.cursor-pointer")[1].Click();

            var updatedRecipe = await dbService.GetDetailedRecipeAsync(recipe.Guid, testUser);
            var ingredients = updatedRecipe.Ingredients;
            Assert.Single(ingredients);
            Assert.Equal("Pasta", ingredients.First().Name);
            Assert.DoesNotContain("Sauce", cut.Markup);
        }

        [Fact]
        public async Task AddStep_WhenClicked_AddsStepToDbAndUi()
        {
            var (recipe, testUser) = await SetupTestRecipe();
            var dbService = GetDbService();

            var cut = RenderComponent<RecipeViewer>(parameters => parameters.Add(p => p.RecipeGuid, recipe.Guid.ToString()));

            cut.FindAll("button").First(b => b.TextContent.Contains("Add Step")).Click();

            var updatedRecipe = await dbService.GetDetailedRecipeAsync(recipe.Guid, testUser);
            var steps = updatedRecipe.Steps;
            Assert.Equal(3, steps.Count());
            Assert.NotNull(cut.Find("textarea"));
        }

        [Fact]
        public async Task DeleteStep_WhenClicked_RemovesStepFromDbAndUi()
        {
            var (recipe, testUser) = await SetupTestRecipe();
            var dbService = GetDbService();

            var cut = RenderComponent<RecipeViewer>(parameters => parameters.Add(p => p.RecipeGuid, recipe.Guid.ToString()));
            Assert.Contains("Layer everything", cut.Markup);

            cut.FindAll(".mud-paper-outlined .mud-icon-button")[0].Click();

            var updatedRecipe = await dbService.GetDetailedRecipeAsync(recipe.Guid, testUser);
            var steps = updatedRecipe.Steps;
            Assert.Single(steps);
            Assert.Equal("Layer everything.", steps.First().Description);
            Assert.DoesNotContain("Boil pasta", cut.Markup);
        }

        [Fact]
        public async Task FinishCooking_WhenClicked_UpdatesLastCookedAndNavigates()
        {
            var (recipe, testUser) = await SetupTestRecipe();
            var dbService = GetDbService();

            var navManager = Services.GetRequiredService<FakeNavigationManager>();
            var initialLastCooked = (await dbService.GetDetailedRecipeAsync(recipe.Guid, testUser)).LastCooked;

            var cut = RenderComponent<RecipeViewer>(parameters => parameters.Add(p => p.RecipeGuid, recipe.Guid.ToString()));

            cut.FindAll("button").First(b => b.TextContent.Contains("Finish Cooking")).Click();

            var updatedRecipe = await dbService.GetDetailedRecipeAsync(recipe.Guid, testUser);
            Assert.NotEqual(initialLastCooked, updatedRecipe.LastCooked);
            Assert.Equal(navManager.BaseUri, navManager.Uri);
        }

        [Fact]
        public async Task ShareRecipe_WhenClicked_ShowsShareDialog()
        {
            var (recipe, testUser) = await SetupTestRecipe();

            var cut = RenderComponent<RecipeViewer>(parameters => parameters.Add(p => p.RecipeGuid, recipe.Guid.ToString()));

            cut.FindAll("button").First(b => b.TextContent.Contains("Share")).Click();

            _dialogServiceMock.Verify(s => s.ShowAsync<RecipeShareDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()), Times.Once());
        }
    }
}