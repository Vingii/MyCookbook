using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// Each test creates its own factory to get an isolated in-memory database.
// Note: the Import endpoint uses CreateExecutionStrategy + BeginTransactionAsync.
// EF Core InMemory returns a no-op transaction, so the round-trip works correctly.
public class ExportControllerTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    public ExportControllerTests()
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
    public async Task Export_ReturnsJsonFile()
    {
        await SeedAsync(ctx =>
        {
            ctx.Recipes.Add(new Recipe { Name = "Omelette", UserName = "devuser" });
        });

        var response = await _client.GetAsync("/api/export");
        response.EnsureSuccessStatusCode();

        Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Export_ThenImport_RoundTripRestoresRecipes()
    {
        // Seed a recipe with ingredients
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var context = await dbFactory.CreateDbContextAsync();

        var recipe = new Recipe { Name = "Pasta Carbonara", UserName = "devuser" };
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        context.Ingredients.AddRange(
            new Ingredient { RecipeId = recipe.Id, UserName = "devuser", Name = "Spaghetti", Amount = "200g", Order = 1 },
            new Ingredient { RecipeId = recipe.Id, UserName = "devuser", Name = "Eggs", Amount = "3", Order = 2 }
        );
        await context.SaveChangesAsync();

        // Export
        var exportResponse = await _client.GetAsync("/api/export");
        exportResponse.EnsureSuccessStatusCode();
        var exportJson = await exportResponse.Content.ReadAsStringAsync();

        // Delete all recipes by importing an empty dataset
        var emptyJson = "[]";
        using var emptyContent = new MultipartFormDataContent();
        var emptyBytes = System.Text.Encoding.UTF8.GetBytes(emptyJson);
        emptyContent.Add(new ByteArrayContent(emptyBytes) { Headers = { ContentType = new("application/json") } }, "file", "empty.json");
        var deleteResponse = await _client.PostAsync("/api/import", emptyContent);
        deleteResponse.EnsureSuccessStatusCode();

        // Verify deleted
        var afterDelete = await _client.GetFromJsonAsync<List<RecipeDto>>("/api/recipes");
        Assert.Empty(afterDelete!);

        // Import the exported data
        using var importContent = new MultipartFormDataContent();
        var exportBytes = System.Text.Encoding.UTF8.GetBytes(exportJson);
        importContent.Add(new ByteArrayContent(exportBytes) { Headers = { ContentType = new("application/json") } }, "file", "export.json");
        var importResponse = await _client.PostAsync("/api/import", importContent);
        importResponse.EnsureSuccessStatusCode();

        // Verify restored
        var restored = await _client.GetFromJsonAsync<List<RecipeDto>>("/api/recipes");
        Assert.Single(restored!);
        Assert.Equal("Pasta Carbonara", restored![0].Name);
        Assert.Equal(2, restored[0].Ingredients.Count);
    }
}
