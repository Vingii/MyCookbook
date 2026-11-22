using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// MC-77: GET /api/planner/ingredients — combined ingredients of the planned recipes.
// Authenticates as "devuser" — see the note on RecipesControllerTests.
public class PlannerIngredientsApiTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    public PlannerIngredientsApiTests()
    {
        _factory = new TestingWebAppFactory<Program>();
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private async Task WithContextAsync(Func<CookbookDatabaseContext, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var context = await dbFactory.CreateDbContextAsync();
        await action(context);
        await context.SaveChangesAsync();
    }

    /// <summary>Plans Gyros on 2026-11-16 and 2026-11-17, Guláš on 2026-11-18 (from fridge) and 2026-11-25.</summary>
    private Task SeedAsync(string owner) => WithContextAsync(ctx =>
    {
        var gyros = new Recipe { Guid = Guid.NewGuid(), Name = "Gyros", UserName = owner };
        gyros.Ingredients.Add(new Ingredient { Name = "Cibule", Amount = "1 ks", Order = 1, UserName = owner });
        gyros.Ingredients.Add(new Ingredient { Name = "Vepřové", Amount = "500 g", Order = 2, UserName = owner });
        var goulash = new Recipe { Guid = Guid.NewGuid(), Name = "Guláš", UserName = owner };
        goulash.Ingredients.Add(new Ingredient { Name = "cibule", Amount = "3 ks", Order = 1, UserName = owner });
        goulash.Ingredients.Add(new Ingredient { Name = "Hovězí", Amount = "1 kg", Order = 2, UserName = owner });
        ctx.Recipes.AddRange(gyros, goulash);

        var id = 1;
        void Plan(Recipe r, string date, bool fridge = false) => ctx.PlannedRecipes.Add(new PlannedRecipe
        {
            Id = id++, UserName = owner, Recipe = r, Date = DateOnly.Parse(date), FromFridge = fridge,
        });
        Plan(gyros, "2026-11-16");
        Plan(gyros, "2026-11-17");
        Plan(goulash, "2026-11-18", fridge: true);
        Plan(goulash, "2026-11-25");
        return Task.CompletedTask;
    });

    private async Task<List<IngredientSummaryDto>> GetAsync(string query)
    {
        var response = await _client.GetAsync($"/api/planner/ingredients?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<IngredientSummaryDto>>())!;
    }

    [Fact]
    public async Task Get_SumsPlannedRecipesInRange_SkippingFromFridge()
    {
        await SeedAsync("devuser");

        var result = await GetAsync("from=2026-11-16&to=2026-11-22");

        Assert.Equal(["Cibule", "Vepřové"], result.Select(r => r.Name));
        Assert.Equal(2m, Assert.Single(result[0].Totals).Quantity);
        Assert.Equal(1000m, Assert.Single(result[1].Totals).Quantity);
        Assert.Equal(["Gyros"], result[0].Recipes);
    }

    [Fact]
    public async Task Get_WiderRange_MergesIngredientsAcrossRecipes()
    {
        await SeedAsync("devuser");

        var result = await GetAsync("from=2026-11-01&to=2026-11-30");

        var onion = result.Single(r => r.Name == "Cibule");
        Assert.Equal(5m, Assert.Single(onion.Totals).Quantity);
        Assert.Equal(["Gyros", "Guláš"], onion.Recipes);
    }

    [Fact]
    public async Task Get_EmptyRange_ReturnsEmptyList()
    {
        await SeedAsync("devuser");
        Assert.Empty(await GetAsync("from=2027-01-01&to=2027-01-31"));
    }

    [Fact]
    public async Task Get_OnlyReturnsOwnPlans()
    {
        await SeedAsync("otheruser");
        Assert.Empty(await GetAsync("from=2026-11-01&to=2026-11-30"));
    }

    [Theory]
    [InlineData("from=2026-11-16")]
    [InlineData("from=nope&to=2026-11-16")]
    [InlineData("from=2026-11-20&to=2026-11-16")]
    public async Task Get_InvalidRange_ReturnsBadRequest(string query)
    {
        var response = await _client.GetAsync($"/api/planner/ingredients?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_IgnoresUserParameter_AndListsOnlyYourIngredients()
    {
        // Sharing a whole collection with ?user=&shareToken= was removed, even with a token that used to work.
        await SeedAsync("anna");
        await WithContextAsync(ctx =>
        {
            ctx.UserPreferences.Add(new UserPreference { UserName = "anna", Key = "ShareToken", Value = "tok" });
            return Task.CompletedTask;
        });

        var result = await GetAsync("from=2026-11-16&to=2026-11-22&user=anna&shareToken=tok");

        Assert.Empty(result);
    }
}
