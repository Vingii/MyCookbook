using Bunit;
using Moq;
using MudBlazor;
using MyCookbook.Components;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Pages;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Pages
{
    public class RecipeBrowserTests : BlazorTestBase
    {
        public RecipeBrowserTests(TestingWebAppFactory<Program> factory) : base(factory)
        {
        }

        [Fact]
        public async Task RecipeBrowser_WhenAuthenticated_DisplaysUserRecipes()
        {
            var testUser = "test-user";
            var otherUser = "other-user";

            var dbService = GetDbService();
            await dbService.CreateRecipeAsync(new Recipe { Name = "User's Own Recipe", UserName = testUser }, testUser);
            await dbService.CreateRecipeAsync(new Recipe { Name = "Another User's Recipe", UserName = otherUser }, otherUser);

            var cut = RenderComponent<RecipeBrowser>();

            Assert.Contains("User's Own Recipe", cut.Markup);
            Assert.DoesNotContain("Another User's Recipe", cut.Markup);
        }

        [Fact]
        public async Task AddRecipe_WhenConfirmed_CreatesRecipeAndRefreshesUi()
        {
            var testUser = "test-user";
            var newRecipeName = "Newly Added Pie";

            var dbService = GetDbService();
            Assert.Empty(await dbService.GetRecipesAsync(testUser));

            var dialogResult = DialogResult.Ok(newRecipeName);
            var mockDialogReference = new Mock<IDialogReference>();
            mockDialogReference.Setup(x => x.Result).ReturnsAsync(dialogResult);

            _dialogServiceMock.Setup(x => x.ShowAsync<NewEntryDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
                              .ReturnsAsync(mockDialogReference.Object);

            var cut = RenderComponent<RecipeBrowser>();

            cut.Find("button.mud-button-filled-primary").Click();
            cut.WaitForState(() => cut.Markup.Contains(newRecipeName));

            var recipes = await dbService.GetRecipesAsync(testUser);
            Assert.Single(recipes);
            Assert.Equal(newRecipeName, recipes.First().Name);
            Assert.Contains(newRecipeName, cut.Markup);
        }

        [Fact]
        public async Task AddRecipe_WhenCanceled_DoesNotCreateRecipe()
        {
            var testUser = "test-user";

            var dbService = GetDbService();
            await dbService.CreateRecipeAsync(new Recipe { Name = "Existing Recipe", UserName = testUser }, testUser);

            var dialogResult = DialogResult.Cancel();
            var mockDialogReference = new Mock<IDialogReference>();
            mockDialogReference.Setup(x => x.Result).ReturnsAsync(dialogResult);
            _dialogServiceMock.Setup(x => x.ShowAsync<NewEntryDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
                              .ReturnsAsync(mockDialogReference.Object);

            var cut = RenderComponent<RecipeBrowser>();

            cut.Find("button.mud-button-filled-primary").Click();

            var recipes = await dbService.GetRecipesAsync(testUser);
            Assert.Single(recipes);
            _dialogServiceMock.Verify(s => s.ShowAsync<NewEntryDialog>(It.IsAny<string>(), It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()), Times.Once());
        }
    }
}