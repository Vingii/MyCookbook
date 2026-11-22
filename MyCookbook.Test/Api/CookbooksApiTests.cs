using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// MC-80: several cookbooks per user. Authenticates as "devuser" — see the note on RecipesControllerTests.
public class CookbooksApiTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    public CookbooksApiTests()
    {
        _factory = new TestingWebAppFactory<Program>();
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private async Task SeedAsync(Action<CookbookDatabaseContext> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var context = await dbFactory.CreateDbContextAsync();
        seed(context);
        await context.SaveChangesAsync();
    }

    private async Task<List<CookbookDto>> GetCookbooksAsync() =>
        (await _client.GetFromJsonAsync<List<CookbookDto>>("/api/cookbooks"))!;

    private async Task<CookbookDto> CreateCookbookAsync(string name, bool showInPlanner = true)
    {
        var response = await _client.PostAsJsonAsync("/api/cookbooks", new SaveCookbookRequest { Name = name, ShowInPlanner = showInPlanner });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CookbookDto>())!;
    }

    private async Task<RecipeDto> CreateRecipeAsync(string name, int? cookbookId)
    {
        var response = await _client.PostAsJsonAsync("/api/recipes", new CreateRecipeRequest { Name = name, CookbookId = cookbookId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecipeDto>())!;
    }

    private async Task<List<string>> RecipeNamesAsync(string query) =>
        (await _client.GetFromJsonAsync<List<RecipeDto>>($"/api/recipes{query}"))!.Select(r => r.Name).Order().ToList();

    [Fact]
    public async Task GetAll_NewUser_GetsOneUnnamedDefaultCookbookHoldingExistingRecipes()
    {
        await SeedAsync(ctx => ctx.Recipes.AddRange(
            new Recipe { Name = "Gyros", UserName = "devuser" },
            new Recipe { Name = "Soup", UserName = "devuser" },
            new Recipe { Name = "Anna's", UserName = "anna" }));

        var cookbook = Assert.Single(await GetCookbooksAsync());

        Assert.True(cookbook.IsDefault);
        Assert.Equal("", cookbook.Name);
        Assert.True(cookbook.ShowInPlanner);
        Assert.Equal(2, cookbook.RecipeCount);
        // Asking again doesn't create another one.
        Assert.Single(await GetCookbooksAsync());
    }

    [Fact]
    public async Task Create_AddsCookbookAfterTheDefault_AndKeepsShowInPlannerFalse()
    {
        var baking = await CreateCookbookAsync("  Baking ", showInPlanner: false);

        var all = await GetCookbooksAsync();
        Assert.Equal(2, all.Count);
        Assert.True(all[0].IsDefault);
        Assert.Equal(baking.Id, all[1].Id);
        Assert.Equal("Baking", all[1].Name);
        Assert.False(all[1].IsDefault);
        Assert.False(all[1].ShowInPlanner);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This name is far too long for a cookbook, way over fifty characters")]
    public async Task Create_InvalidName_ReturnsBadRequest(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/cookbooks", new SaveCookbookRequest { Name = name });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_RenamesAndHidesFromPlanner()
    {
        var defaultId = (await GetCookbooksAsync())[0].Id;

        var response = await _client.PutAsJsonAsync($"/api/cookbooks/{defaultId}", new SaveCookbookRequest { Name = "Home", ShowInPlanner = false });

        response.EnsureSuccessStatusCode();
        var cookbook = Assert.Single(await GetCookbooksAsync());
        Assert.Equal("Home", cookbook.Name);
        Assert.False(cookbook.ShowInPlanner);
    }

    [Fact]
    public async Task Update_AnotherUsersCookbook_ReturnsNotFound()
    {
        var annas = new Cookbook { UserName = "anna", Name = "Anna's" };
        await SeedAsync(ctx => ctx.Cookbooks.Add(annas));

        var response = await _client.PutAsJsonAsync($"/api/cookbooks/{annas.Id}", new SaveCookbookRequest { Name = "Mine" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Recipes_FilteredByCookbook_DefaultIncludesRecipesWithoutOne()
    {
        await SeedAsync(ctx => ctx.Recipes.Add(new Recipe { Name = "Legacy", UserName = "devuser" }));
        var defaultId = (await GetCookbooksAsync())[0].Id;
        var baking = await CreateCookbookAsync("Baking");
        await CreateRecipeAsync("Bread", baking.Id);
        await CreateRecipeAsync("Gyros", null);

        Assert.Equal(["Gyros", "Legacy"], await RecipeNamesAsync($"?cookbook={defaultId}"));
        Assert.Equal(["Bread"], await RecipeNamesAsync($"?cookbook={baking.Id}"));
        Assert.Equal(["Bread", "Gyros", "Legacy"], await RecipeNamesAsync(""));
    }

    [Fact]
    public async Task Recipes_AnotherUsersCookbook_ListIsNotFound_AndCreateIsBadRequest()
    {
        var annas = new Cookbook { UserName = "anna", Name = "Anna's" };
        await SeedAsync(ctx => ctx.Cookbooks.Add(annas));

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/recipes?cookbook={annas.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/recipes/random?cookbook={annas.Id}")).StatusCode);
        var create = await _client.PostAsJsonAsync("/api/recipes", new CreateRecipeRequest { Name = "X", CookbookId = annas.Id });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Random_PicksFromTheCookbook()
    {
        var baking = await CreateCookbookAsync("Baking");
        await CreateRecipeAsync("Bread", baking.Id);
        await CreateRecipeAsync("Gyros", null);

        for (var i = 0; i < 5; i++)
        {
            var random = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/random?cookbook={baking.Id}");
            Assert.Equal("Bread", random!.Name);
        }
    }

    [Fact]
    public async Task UpdateRecipe_MovesItToAnotherCookbook_OmittedKeepsIt()
    {
        var baking = await CreateCookbookAsync("Baking");
        var bread = await CreateRecipeAsync("Bread", null);

        (await _client.PutAsJsonAsync($"/api/recipes/{bread.Guid}", new UpdateRecipeRequest { Name = "Bread", CookbookId = baking.Id })).EnsureSuccessStatusCode();
        Assert.Equal(baking.Id, (await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{bread.Guid}"))!.CookbookId);

        (await _client.PutAsJsonAsync($"/api/recipes/{bread.Guid}", new UpdateRecipeRequest { Name = "Rye bread" })).EnsureSuccessStatusCode();
        var updated = (await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{bread.Guid}"))!;
        Assert.Equal("Rye bread", updated.Name);
        Assert.Equal(baking.Id, updated.CookbookId);
    }

    [Fact]
    public async Task Clone_OwnRecipe_StaysInItsCookbook_UnlessAnotherIsGiven()
    {
        var defaultId = (await GetCookbooksAsync())[0].Id;
        var baking = await CreateCookbookAsync("Baking");
        var bread = await CreateRecipeAsync("Bread", baking.Id);

        var copy = await (await _client.PostAsync($"/api/recipes/{bread.Guid}/clone", null)).Content.ReadFromJsonAsync<RecipeDto>();
        var moved = await (await _client.PostAsync($"/api/recipes/{bread.Guid}/clone?cookbook={defaultId}", null)).Content.ReadFromJsonAsync<RecipeDto>();

        Assert.Equal(baking.Id, copy!.CookbookId);
        Assert.Equal(defaultId, moved!.CookbookId);
    }

    [Fact]
    public async Task Clone_AnotherUsersRecipe_GoesToTheDefaultCookbook()
    {
        var annas = new Cookbook { UserName = "anna", Name = "Anna's" };
        var guid = Guid.NewGuid();
        await SeedAsync(ctx => ctx.Recipes.Add(new Recipe { Guid = guid, Name = "Gyros", UserName = "anna", Cookbook = annas }));

        var copy = await (await _client.PostAsync($"/api/recipes/{guid}/clone", null)).Content.ReadFromJsonAsync<RecipeDto>();

        Assert.Null(copy!.CookbookId);
    }

    [Fact]
    public async Task Delete_EmptyCookbook_Succeeds()
    {
        var baking = await CreateCookbookAsync("Baking");

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/cookbooks/{baking.Id}")).StatusCode);
        Assert.Single(await GetCookbooksAsync());
    }

    [Fact]
    public async Task Delete_CookbookWithRecipes_IsConflict()
    {
        var baking = await CreateCookbookAsync("Baking");
        await CreateRecipeAsync("Bread", baking.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/cookbooks/{baking.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_DefaultCookbookWithRecipesWithoutOne_IsConflict()
    {
        await SeedAsync(ctx => ctx.Recipes.Add(new Recipe { Name = "Legacy", UserName = "devuser" }));
        var defaultId = (await GetCookbooksAsync())[0].Id;
        await CreateCookbookAsync("Baking");

        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/cookbooks/{defaultId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_LastCookbook_IsConflict()
    {
        var defaultId = (await GetCookbooksAsync())[0].Id;

        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/cookbooks/{defaultId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_EmptyDefault_MakesTheNextOneTheDefault()
    {
        var defaultId = (await GetCookbooksAsync())[0].Id;
        var baking = await CreateCookbookAsync("Baking");

        (await _client.DeleteAsync($"/api/cookbooks/{defaultId}")).EnsureSuccessStatusCode();

        var cookbook = Assert.Single(await GetCookbooksAsync());
        Assert.Equal(baking.Id, cookbook.Id);
        Assert.True(cookbook.IsDefault);
    }

    [Fact]
    public async Task Delete_UnknownCookbook_IsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/cookbooks/9999")).StatusCode);
    }

    [Fact]
    public async Task PlannerRecipes_LeaveOutCookbooksHiddenFromThePlanner()
    {
        var defaultId = (await GetCookbooksAsync())[0].Id;
        var testing = await CreateCookbookAsync("Testing", showInPlanner: false);
        await CreateRecipeAsync("Experiment", testing.Id);
        await CreateRecipeAsync("Gyros", null);

        Assert.Equal(["Gyros"], await RecipeNamesAsync("?planner=true"));

        // Hiding the default cookbook hides recipes without a cookbook too.
        await _client.PutAsJsonAsync($"/api/cookbooks/{defaultId}", new SaveCookbookRequest { Name = "Home", ShowInPlanner = false });
        Assert.Empty(await RecipeNamesAsync("?planner=true"));
    }

    [Fact]
    public async Task Suggestions_LeaveOutCookbooksHiddenFromThePlanner()
    {
        var testing = await CreateCookbookAsync("Testing", showInPlanner: false);
        await CreateRecipeAsync("Experiment", testing.Id);
        await CreateRecipeAsync("Gyros", null);

        var result = await _client.GetFromJsonAsync<List<PlanSuggestionDto>>("/api/planner/suggestions?from=2030-01-05&to=2030-01-05");

        Assert.Equal(["Gyros"], result!.Select(s => s.RecipeName));
    }

    [Fact]
    public async Task Import_KeepsOwnCookbooks_AndDropsUnknownOnes()
    {
        var baking = await CreateCookbookAsync("Baking");

        using var content = new MultipartFormDataContent();
        var json = $$"""[{"Name":"Bread","CookbookId":{{baking.Id}}},{"Name":"Gyros","CookbookId":9999}]""";
        content.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(json)) { Headers = { ContentType = new("application/json") } }, "file", "export.json");
        (await _client.PostAsync("/api/import", content)).EnsureSuccessStatusCode();

        var recipes = (await _client.GetFromJsonAsync<List<RecipeDto>>("/api/recipes"))!.ToDictionary(r => r.Name);
        Assert.Equal(baking.Id, recipes["Bread"].CookbookId);
        Assert.Null(recipes["Gyros"].CookbookId);
    }
    // MC-81: what recipes in a cookbook show.
    [Fact]
    public async Task DisplaySettings_ShowEverythingByDefault()
    {
        var baking = await CreateCookbookAsync("Baking");

        Assert.True(baking.ShowDuration && baking.ShowLastCooked && baking.ShowServings && baking.ShowStepTimeline);
        var all = await GetCookbooksAsync();
        Assert.All(all, c => Assert.True(c.ShowDuration && c.ShowLastCooked && c.ShowServings && c.ShowStepTimeline));
    }

    [Fact]
    public async Task DisplaySettings_AreSaved_AndKeptWhenAnUpdateLeavesThemOut()
    {
        var defaultId = (await GetCookbooksAsync())[0].Id;

        await _client.PutAsJsonAsync($"/api/cookbooks/{defaultId}", new SaveCookbookRequest
        {
            Name = "Home", ShowDuration = false, ShowLastCooked = false, ShowServings = true, ShowStepTimeline = false,
        });
        // An older client sends only the name and the planner setting.
        await _client.PutAsJsonAsync($"/api/cookbooks/{defaultId}", new SaveCookbookRequest { Name = "Home 2", ShowInPlanner = false });

        var cookbook = Assert.Single(await GetCookbooksAsync());
        Assert.Equal("Home 2", cookbook.Name);
        Assert.False(cookbook.ShowInPlanner);
        Assert.False(cookbook.ShowDuration);
        Assert.False(cookbook.ShowLastCooked);
        Assert.True(cookbook.ShowServings);
        Assert.False(cookbook.ShowStepTimeline);
    }

    [Fact]
    public async Task Create_WithDisplaySettings_SavesThem()
    {
        var response = await _client.PostAsJsonAsync("/api/cookbooks", new SaveCookbookRequest { Name = "Drinks", ShowServings = false });
        var created = (await response.Content.ReadFromJsonAsync<CookbookDto>())!;

        Assert.False(created.ShowServings);
        Assert.True(created.ShowDuration);
        Assert.False((await GetCookbooksAsync()).Single(c => c.Id == created.Id).ShowServings);
    }

    [Fact]
    public async Task RecipeDetail_CarriesItsCookbooksDisplaySettings()
    {
        var bar = await CreateCookbookAsync("Bar");
        await _client.PutAsJsonAsync($"/api/cookbooks/{bar.Id}", new SaveCookbookRequest { Name = "Bar", ShowServings = false, ShowStepTimeline = false });
        var mojito = await CreateRecipeAsync("Mojito", bar.Id);
        var gyros = await CreateRecipeAsync("Gyros", null);

        var mojitoDisplay = (await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{mojito.Guid}"))!.Display!;
        var gyrosDisplay = (await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{gyros.Guid}"))!.Display!;

        Assert.False(mojitoDisplay.Servings);
        Assert.False(mojitoDisplay.StepTimeline);
        Assert.True(mojitoDisplay.Duration);
        Assert.True(mojitoDisplay.LastCooked);
        // Gyros has no cookbook: it is in the default one, which shows everything.
        Assert.True(gyrosDisplay.Servings && gyrosDisplay.StepTimeline && gyrosDisplay.Duration && gyrosDisplay.LastCooked);
        // Lists don't carry it.
        Assert.All((await _client.GetFromJsonAsync<List<RecipeDto>>("/api/recipes"))!, r => Assert.Null(r.Display));
    }

    [Fact]
    public async Task SharedRecipe_ShowsAsTheOwnersCookbookDoes_WithoutRevealingTheCookbook()
    {
        var guid = Guid.NewGuid();
        var annasBar = new Cookbook { UserName = "anna", Name = "Bar", ShowLastCooked = false, ShowDuration = false };
        await SeedAsync(ctx => ctx.Recipes.Add(new Recipe { Guid = guid, Name = "Mojito", UserName = "anna", Cookbook = annasBar }));

        var shared = (await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/shared/{guid}"))!;

        Assert.Null(shared.CookbookId);
        Assert.False(shared.Display!.LastCooked);
        Assert.False(shared.Display.Duration);
        Assert.True(shared.Display.Servings);
    }

    [Fact]
    public async Task SharedRecipe_OwnerWithoutCookbooks_ShowsEverything()
    {
        var guid = Guid.NewGuid();
        await SeedAsync(ctx => ctx.Recipes.Add(new Recipe { Guid = guid, Name = "Mojito", UserName = "anna" }));

        var display = (await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/shared/{guid}"))!.Display!;

        Assert.True(display.Duration && display.LastCooked && display.Servings && display.StepTimeline);
        // Reading a shared recipe doesn't create cookbooks for its owner.
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var ctx = await dbFactory.CreateDbContextAsync();
        Assert.False(await ctx.Cookbooks.AnyAsync(c => c.UserName == "anna"));
    }
}
