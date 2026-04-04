using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// Each test creates its own factory to get an isolated in-memory database.
// All tests authenticate as "devuser" (set by DEV_AUTO_LOGIN in appsettings.Development.json,
// which is activated by TestingWebAppFactory.UseEnvironment("Development")).
// Note: testing a 401 response is not possible with this factory because DEV_AUTO_LOGIN
// always fires in the Development environment.
public class RecipesControllerTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    public RecipesControllerTests()
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

    [Fact]
    public async Task GetAll_NoFilter_ReturnsOnlyCurrentUserRecipes()
    {
        await SeedAsync(ctx =>
        {
            ctx.Recipes.Add(new Recipe { Name = "Devuser Recipe 1", UserName = "devuser" });
            ctx.Recipes.Add(new Recipe { Name = "Devuser Recipe 2", UserName = "devuser" });
            ctx.Recipes.Add(new Recipe { Name = "Devuser Recipe 3", UserName = "devuser" });
            ctx.Recipes.Add(new Recipe { Name = "Other User Recipe", UserName = "otheruser" });
        });

        var response = await _client.GetAsync("/api/recipes");
        response.EnsureSuccessStatusCode();

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeDto>>();
        Assert.Equal(3, recipes!.Count);
        Assert.All(recipes, r => Assert.NotEqual("Other User Recipe", r.Name));
    }

    [Fact]
    public async Task GetAll_WithSearchFilter_ReturnsMatchingOnly()
    {
        await SeedAsync(ctx =>
        {
            ctx.Recipes.Add(new Recipe { Name = "Pancakes", UserName = "devuser" });
            ctx.Recipes.Add(new Recipe { Name = "Pizza", UserName = "devuser" });
        });

        var response = await _client.GetAsync("/api/recipes?search=pan");
        response.EnsureSuccessStatusCode();

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeDto>>();
        Assert.Single(recipes!);
        Assert.Equal("Pancakes", recipes![0].Name);
    }

    [Fact]
    public async Task GetAll_WithCategoryFilter_ReturnsMatchingOnly()
    {
        await SeedAsync(ctx =>
        {
            ctx.Recipes.Add(new Recipe { Name = "Tomato Soup", Category = "Soup", UserName = "devuser" });
            ctx.Recipes.Add(new Recipe { Name = "Brownie", Category = "Dessert", UserName = "devuser" });
        });

        var response = await _client.GetAsync("/api/recipes?category=soup");
        response.EnsureSuccessStatusCode();

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeDto>>();
        Assert.Single(recipes!);
        Assert.Equal("Tomato Soup", recipes![0].Name);
    }

    [Fact]
    public async Task GetAll_WithTagFilter_ReturnsMatchingOnly()
    {
        // Seed recipe first to get the generated Id, then add tag
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var context = await dbFactory.CreateDbContextAsync();

        var taggedRecipe = new Recipe { Name = "Lentil Stew", UserName = "devuser" };
        var plainRecipe = new Recipe { Name = "Roast Chicken", UserName = "devuser" };
        context.Recipes.AddRange(taggedRecipe, plainRecipe);
        await context.SaveChangesAsync();

        context.Tags.Add(new Tag { RecipeId = taggedRecipe.Id, UserName = "devuser", Name = "vegetarian" });
        await context.SaveChangesAsync();

        var response = await _client.GetAsync("/api/recipes?tag=vegetarian");
        response.EnsureSuccessStatusCode();

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeDto>>();
        Assert.Single(recipes!);
        Assert.Equal("Lentil Stew", recipes![0].Name);
    }

    [Fact]
    public async Task GetById_ExistingRecipe_Returns200WithName()
    {
        var guid = Guid.NewGuid();
        await SeedAsync(ctx =>
        {
            ctx.Recipes.Add(new Recipe { Guid = guid, Name = "Spaghetti", UserName = "devuser" });
        });

        var response = await _client.GetAsync($"/api/recipes/{guid}");
        response.EnsureSuccessStatusCode();

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        Assert.Equal("Spaghetti", recipe!.Name);
    }

    [Fact]
    public async Task GetById_UnknownGuid_Returns404()
    {
        var response = await _client.GetAsync($"/api/recipes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
