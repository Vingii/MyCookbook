using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// MC-76: GET /api/planner/suggestions. Authenticates as "devuser" — see the note on RecipesControllerTests.
public class PlannerSuggestionsApiTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    public PlannerSuggestionsApiTests()
    {
        _factory = new TestingWebAppFactory<Program>();
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private async Task SeedAsync(string owner)
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var ctx = await dbFactory.CreateDbContextAsync();
        var gyros = new Recipe { Guid = Guid.NewGuid(), Name = "Gyros", Category = "Greek", UserName = owner };
        var goulash = new Recipe { Guid = Guid.NewGuid(), Name = "Guláš", Category = "Czech", UserName = owner };
        var soup = new Recipe { Guid = Guid.NewGuid(), Name = "Soup", Category = "Soups", UserName = owner };
        ctx.Recipes.AddRange(gyros, goulash, soup);
        ctx.PlannedRecipes.Add(new PlannedRecipe { Id = 1, UserName = owner, Recipe = gyros, Date = new DateOnly(2030, 1, 3) });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_SuggestsRecipesNotPlannedAroundThatDay()
    {
        await SeedAsync("devuser");

        var result = await _client.GetFromJsonAsync<List<PlanSuggestionDto>>("/api/planner/suggestions?date=2030-01-05");

        Assert.Equal(["Guláš", "Soup"], result!.Select(s => s.RecipeName).OrderBy(n => n));
        Assert.All(result, s => Assert.Contains(s.Reasons, r => r.Code == "neverCooked"));
    }

    [Fact]
    public async Task Get_RespectsCount()
    {
        await SeedAsync("devuser");
        var result = await _client.GetFromJsonAsync<List<PlanSuggestionDto>>("/api/planner/suggestions?date=2030-02-01&count=2");
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public async Task Get_InvalidDate_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/planner/suggestions?date=nope");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_OtherUserWithoutShareToken_IsForbidden()
    {
        await SeedAsync("anna");
        var response = await _client.GetAsync("/api/planner/suggestions?date=2030-01-05&user=anna");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
