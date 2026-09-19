using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// End-to-end coverage for wiki-style [[Recipe name]] links between recipes.
// Authenticates as "devuser" — see the note on RecipesControllerTests.
public class RecipeLinksApiTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    private readonly Guid _gyrosGuid = Guid.NewGuid();
    private readonly Guid _tzatzikiGuid = Guid.NewGuid();

    public RecipeLinksApiTests()
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

    /// <summary>Seeds a "Gyros" recipe whose last step links to "Tzatziki", both owned by <paramref name="user"/>.</summary>
    private Task SeedLinkedPairAsync(string user = "devuser", string linkText = "Tzatziki") => SeedAsync(ctx =>
    {
        var gyros = new Recipe { Guid = _gyrosGuid, Name = "Gyros", UserName = user };
        gyros.Steps.Add(new Step { Description = "Grill the meat.", Order = 1, UserName = user });
        gyros.Steps.Add(new Step { Description = $"Serve with [[{linkText}]].", Order = 2, UserName = user });
        ctx.Recipes.Add(gyros);
        ctx.Recipes.Add(new Recipe { Guid = _tzatzikiGuid, Name = "Tzatziki", UserName = user });
    });

    [Fact]
    public async Task GetById_StepLinkingToAnotherRecipe_ResolvesToItsGuid()
    {
        await SeedLinkedPairAsync();

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");

        var link = Assert.Single(recipe!.Links);
        Assert.Equal("Tzatziki", link.Text);
        Assert.Equal("Tzatziki", link.Name);
        Assert.Equal(_tzatzikiGuid, link.Guid);
    }

    [Fact]
    public async Task GetById_LinkTextDiffersInCaseAndDiacritics_StillResolves()
    {
        await SeedAsync(ctx =>
        {
            var gyros = new Recipe { Guid = _gyrosGuid, Name = "Gyros", UserName = "devuser" };
            gyros.Steps.Add(new Step { Description = "Serve with [[bramborova KASE]].", Order = 1, UserName = "devuser" });
            ctx.Recipes.Add(gyros);
            ctx.Recipes.Add(new Recipe { Guid = _tzatzikiGuid, Name = "Bramborová kaše", UserName = "devuser" });
        });

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");

        var link = Assert.Single(recipe!.Links);
        Assert.Equal("bramborova KASE", link.Text);
        Assert.Equal("Bramborová kaše", link.Name);
        Assert.Equal(_tzatzikiGuid, link.Guid);
    }

    [Fact]
    public async Task GetById_LinkToMissingRecipe_IsNotReturned()
    {
        await SeedLinkedPairAsync(linkText: "Baklava");

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");

        Assert.Empty(recipe!.Links);
    }

    [Fact]
    public async Task GetById_LinkToAnotherUsersRecipe_IsNotReturned()
    {
        await SeedAsync(ctx =>
        {
            var gyros = new Recipe { Guid = _gyrosGuid, Name = "Gyros", UserName = "devuser" };
            gyros.Steps.Add(new Step { Description = "Serve with [[Tzatziki]].", Order = 1, UserName = "devuser" });
            ctx.Recipes.Add(gyros);
            ctx.Recipes.Add(new Recipe { Guid = _tzatzikiGuid, Name = "Tzatziki", UserName = "otheruser" });
        });

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");

        Assert.Empty(recipe!.Links);
    }

    [Fact]
    public async Task GetById_LinkedRecipe_ListsTheReferringRecipeInUsedIn()
    {
        await SeedLinkedPairAsync();

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_tzatzikiGuid}");

        var backlink = Assert.Single(recipe!.UsedIn);
        Assert.Equal("Gyros", backlink.Name);
        Assert.Equal(_gyrosGuid, backlink.Guid);
    }

    [Fact]
    public async Task GetById_RecipeReferencedTwiceByOneRecipe_IsListedOnceInUsedIn()
    {
        await SeedAsync(ctx =>
        {
            var gyros = new Recipe { Guid = _gyrosGuid, Name = "Gyros", UserName = "devuser" };
            gyros.Steps.Add(new Step { Description = "Prepare [[Tzatziki]].", Order = 1, UserName = "devuser" });
            gyros.Steps.Add(new Step { Description = "Serve with [[tzatziki]].", Order = 2, UserName = "devuser" });
            ctx.Recipes.Add(gyros);
            ctx.Recipes.Add(new Recipe { Guid = _tzatzikiGuid, Name = "Tzatziki", UserName = "devuser" });
        });

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_tzatzikiGuid}");

        Assert.Single(recipe!.UsedIn);
    }

    [Fact]
    public async Task GetById_AnotherUsersRecipeLinksToMine_IsNotListedInUsedIn()
    {
        await SeedAsync(ctx =>
        {
            var theirs = new Recipe { Guid = _gyrosGuid, Name = "Gyros", UserName = "otheruser" };
            theirs.Steps.Add(new Step { Description = "Serve with [[Tzatziki]].", Order = 1, UserName = "otheruser" });
            ctx.Recipes.Add(theirs);
            ctx.Recipes.Add(new Recipe { Guid = _tzatzikiGuid, Name = "Tzatziki", UserName = "devuser" });
        });

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_tzatzikiGuid}");

        Assert.Empty(recipe!.UsedIn);
    }

    [Fact]
    public async Task GetShared_ResolvesLinksButOmitsBacklinks()
    {
        await SeedLinkedPairAsync();

        var gyros = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/shared/{_gyrosGuid}");
        var tzatziki = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/shared/{_tzatzikiGuid}");

        Assert.Equal(_tzatzikiGuid, Assert.Single(gyros!.Links).Guid);
        Assert.Empty(tzatziki!.UsedIn);
    }

    [Fact]
    public async Task GetAll_DoesNotPopulateLinks()
    {
        await SeedLinkedPairAsync();

        var recipes = await _client.GetFromJsonAsync<List<RecipeDto>>("/api/recipes");

        Assert.All(recipes!, r => Assert.Empty(r.Links));
    }

    [Fact]
    public async Task GetById_DisplayPipeReferenceLink_ResolvesOnTheReference()
    {
        await SeedLinkedPairAsync(linkText: "tzatzikem|Tzatziki");

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");

        var link = Assert.Single(recipe!.Links);
        Assert.Equal("Tzatziki", link.Text);
        Assert.Equal(_tzatzikiGuid, link.Guid);
    }

    [Fact]
    public async Task CreateStep_ShorthandLink_IsExpandedToTheTwoPartForm()
    {
        await SeedAsync(ctx => ctx.Recipes.Add(new Recipe { Guid = _gyrosGuid, Name = "Gyros", UserName = "devuser" }));

        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{_gyrosGuid}/steps",
            new { Description = "Serve with [[Tzatziki]].", StepType = "Active" });
        response.EnsureSuccessStatusCode();

        var recipe = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");
        Assert.Equal("Serve with [[Tzatziki|Tzatziki]].", Assert.Single(recipe!.Steps).Description);
    }

    [Fact]
    public async Task UpdateStep_ShorthandLink_IsExpandedToTheTwoPartForm()
    {
        await SeedLinkedPairAsync();
        var before = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");
        var stepId = before!.Steps.First(s => s.Description.Contains("[[")).Id;

        var response = await _client.PutAsJsonAsync(
            $"/api/recipes/{_gyrosGuid}/steps/{stepId}",
            new { Description = "Dollop on the [[Tzatziki]].", StepType = "Active" });
        response.EnsureSuccessStatusCode();

        var after = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");
        Assert.Contains(after!.Steps, s => s.Description == "Dollop on the [[Tzatziki|Tzatziki]].");
    }

    [Fact]
    public async Task UpdateRecipe_Rename_RepointsLinksAndKeepsTheDisplayText()
    {
        await SeedLinkedPairAsync(linkText: "tzatzikem|Tzatziki");

        var response = await _client.PutAsJsonAsync(
            $"/api/recipes/{_tzatzikiGuid}",
            new { Name = "Okurkový dip", Servings = 4 });
        response.EnsureSuccessStatusCode();

        var gyros = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");
        Assert.Contains(gyros!.Steps, s => s.Description == "Serve with [[tzatzikem|Okurkový dip]].");
        Assert.Equal(_tzatzikiGuid, Assert.Single(gyros.Links).Guid);
    }

    [Fact]
    public async Task UpdateRecipe_Rename_ConvertsShorthandLinksAndKeepsTheOldNameVisible()
    {
        await SeedLinkedPairAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/recipes/{_tzatzikiGuid}",
            new { Name = "Cucumber dip", Servings = 4 });
        response.EnsureSuccessStatusCode();

        var gyros = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");
        Assert.Contains(gyros!.Steps, s => s.Description == "Serve with [[Tzatziki|Cucumber dip]].");
        Assert.Equal(_tzatzikiGuid, Assert.Single(gyros.Links).Guid);
    }

    [Fact]
    public async Task UpdateRecipe_Rename_DoesNotTouchAnotherUsersSteps()
    {
        await SeedAsync(ctx =>
        {
            var theirs = new Recipe { Guid = _gyrosGuid, Name = "Gyros", UserName = "otheruser" };
            theirs.Steps.Add(new Step { Description = "Serve with [[Tzatziki|Tzatziki]].", Order = 1, UserName = "otheruser" });
            ctx.Recipes.Add(theirs);
            ctx.Recipes.Add(new Recipe { Guid = _tzatzikiGuid, Name = "Tzatziki", UserName = "devuser" });
        });

        var response = await _client.PutAsJsonAsync(
            $"/api/recipes/{_tzatzikiGuid}",
            new { Name = "Cucumber dip", Servings = 4 });
        response.EnsureSuccessStatusCode();

        var theirGyros = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/shared/{_gyrosGuid}");
        Assert.Contains(theirGyros!.Steps, s => s.Description == "Serve with [[Tzatziki|Tzatziki]].");
    }

    [Fact]
    public async Task UpdateRecipe_WithoutRenaming_LeavesStepsAlone()
    {
        await SeedLinkedPairAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/recipes/{_tzatzikiGuid}",
            new { Name = "Tzatziki", Servings = 8 });
        response.EnsureSuccessStatusCode();

        var gyros = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{_gyrosGuid}");
        Assert.Contains(gyros!.Steps, s => s.Description == "Serve with [[Tzatziki]].");
    }

    [Fact]
    public async Task GetNames_ReturnsCurrentUsersRecipeNamesSorted()
    {
        await SeedAsync(ctx =>
        {
            ctx.Recipes.Add(new Recipe { Name = "Tzatziki", UserName = "devuser" });
            ctx.Recipes.Add(new Recipe { Name = "Gyros", UserName = "devuser" });
            ctx.Recipes.Add(new Recipe { Name = "Baklava", UserName = "otheruser" });
        });

        var names = await _client.GetFromJsonAsync<List<string>>("/api/recipes/names");

        Assert.Equal(["Gyros", "Tzatziki"], names);
    }
}
