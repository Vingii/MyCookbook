using Bunit;
using Moq;
using MudBlazor;
using MyCookbook.Components;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Components
{
    public class RecipeTableTests : BlazorTestBase
    {
        public RecipeTableTests(TestingWebAppFactory<Program> factory) : base(factory)
        {
        }

        [Fact]
        public async Task RecipeTable_WhenRenderedWithData_DisplaysRecipesCorrectly()
        {
            var dbService = GetDbService();
            var testUser = "test-user";

            await dbService.CreateRecipeAsync(new Recipe { Name = "Owned Pancake", UserName = testUser }, testUser);
            await dbService.CreateRecipeAsync(new Recipe { Name = "Shared Taco", UserName = "another-user" }, "another-user");

            Func<Task<IEnumerable<Recipe>>> recipeGetter = async () => await dbService.GetRecipesAsync(testUser);

            var cut = RenderComponent<RecipeTable>(parameters => parameters
                .Add(p => p.UserIdentityName, testUser)
                .Add(p => p.TableName, "My Recipes")
                .Add(p => p.RecipeGetter, recipeGetter)
            );

            await cut.Instance.Load();

            Assert.Contains("My Recipes", cut.Markup);
            Assert.Contains("Owned Pancake", cut.Markup);
            Assert.DoesNotContain("Shared Taco", cut.Markup);
        }

        [Fact]
        public async Task ClickingOwnedRecipe_NavigatesToCorrectUrl()
        {
            var dbService = GetDbService();
            var navManager = GetNavManager();
            var testUser = "test-user";

            var ownedRecipe = await dbService.CreateRecipeAsync(new Recipe { Name = "Owned Pancake", UserName = testUser }, testUser);

            Func<Task<IEnumerable<Recipe>>> recipeGetter = async () => await dbService.GetRecipesAsync(testUser);

            var cut = RenderComponent<RecipeTable>(parameters => parameters
                .Add(p => p.UserIdentityName, testUser)
                .Add(p => p.RecipeGetter, recipeGetter)
            );

            await cut.Instance.Load();

            cut.FindAll("tbody td").First(x => x.TextContent.Contains(ownedRecipe.Name)).MouseDown();

            Assert.Equal($"Recipe/{ownedRecipe.Guid}", navManager.Uri.Replace(navManager.BaseUri, ""));
        }

        [Fact]
        public async Task ClickingSharedRecipe_NavigatesToCorrectSharedUrl()
        {
            var dbService = GetDbService();
            var navManager = GetNavManager();
            var testUser = "test-user";
            var otherUser = "other-user";

            var sharedRecipe = await dbService.CreateRecipeAsync(new Recipe { Name = "Shared Taco", UserName = otherUser, Guid = Guid.NewGuid() }, otherUser);

            Func<Task<IEnumerable<Recipe>>> recipeGetter = async () => new List<Recipe> { sharedRecipe };

            var cut = RenderComponent<RecipeTable>(parameters => parameters
                .Add(p => p.UserIdentityName, testUser)
                .Add(p => p.RecipeGetter, recipeGetter)
            );

            await cut.Instance.Load();

            cut.FindAll("tbody td").First(x => x.TextContent == sharedRecipe.Name).MouseDown();

            Assert.Equal($"Recipe/Shared/{sharedRecipe.Guid}", navManager.Uri.Replace(navManager.BaseUri, ""));
        }

        [Fact]
        public async Task DeleteRecipe_WhenConfirmed_DeletesFromDbAndRefreshesUi()
        {
            var dbService = GetDbService();
            var testUser = "test-user";

            await dbService.CreateRecipeAsync(new Recipe { Name = "Recipe To Be Deleted", UserName = testUser }, testUser);

            Func<Task<IEnumerable<Recipe>>> recipeGetter = async () => await dbService.GetRecipesAsync(testUser);

            Assert.Single(await dbService.GetRecipesAsync(testUser));

            var dialogResult = DialogResult.Ok(true);
            var mockDialogReference = new Mock<IDialogReference>();
            mockDialogReference.Setup(x => x.Result).ReturnsAsync(dialogResult);
            _dialogServiceMock.Setup(x => x.ShowAsync<ConfirmationDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
                              .ReturnsAsync(mockDialogReference.Object);

            var cut = RenderComponent<RecipeTable>(parameters => parameters
                .Add(p => p.Editable, true)
                .Add(p => p.UserIdentityName, testUser)
                .Add(p => p.RecipeGetter, recipeGetter)
            );

            await cut.Instance.Load();

            cut.FindAll(".mud-icon-root.ma-0")[0].Click();

            var recipesAfter = await dbService.GetRecipesAsync(testUser);
            Assert.Empty(recipesAfter);
            Assert.DoesNotContain("Recipe To Be Deleted", cut.Markup);
        }

        [Fact]
        public async Task DeleteRecipe_WhenAttemptingToDeleteAnotherUsersRecipe_DoesNotDeleteFromDb()
        {
            var dbService = GetDbService();
            var testUser = "test-user";
            var otherUser = "other-user";

            var sharedRecipe = await dbService.CreateRecipeAsync(new Recipe { Name = "Another User's Recipe", UserName = otherUser }, otherUser);

            Func<Task<IEnumerable<Recipe>>> recipeGetter = async () => new List<Recipe> { sharedRecipe };

            Assert.Single(await dbService.GetRecipesAsync(otherUser));

            var dialogResult = DialogResult.Ok(true);
            var mockDialogReference = new Mock<IDialogReference>();
            mockDialogReference.Setup(x => x.Result).ReturnsAsync(dialogResult);
            _dialogServiceMock.Setup(x => x.ShowAsync<ConfirmationDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
                              .ReturnsAsync(mockDialogReference.Object);

            var cut = RenderComponent<RecipeTable>(parameters => parameters
                .Add(p => p.Editable, true)
                .Add(p => p.UserIdentityName, testUser)
                .Add(p => p.RecipeGetter, recipeGetter)
            );

            await cut.Instance.Load();

            cut.FindAll(".mud-icon-root.ma-0")[0].Click();

            var recipesAfter = await dbService.GetRecipesAsync(otherUser);
            Assert.Single(recipesAfter);
            Assert.Equal(sharedRecipe.Id, recipesAfter.First().Id);
        }
    }
}