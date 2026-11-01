using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// MC-70: cloning a recipe someone shared with you into your own cookbook.
// Authenticates as "devuser" — see the note on RecipesControllerTests.
public class CloneRecipeApiTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    private readonly Guid _guid = Guid.NewGuid();

    public CloneRecipeApiTests()
    {
        _factory = new TestingWebAppFactory<Program>();
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private async Task<T> WithContextAsync<T>(Func<CookbookDatabaseContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var context = await dbFactory.CreateDbContextAsync();
        return await action(context);
    }

    /// <summary>Seeds a "Gyros" recipe with one ingredient, step and tag, owned by <paramref name="owner"/>.</summary>
    private Task SeedGyrosAsync(string owner) => WithContextAsync(async ctx =>
    {
        var recipe = new Recipe
        {
            Guid = _guid, Name = "Gyros", Category = "Greek", Servings = 4, UserName = owner,
            LastCooked = new DateTime(2026, 10, 1),
        };
        recipe.Ingredients.Add(new Ingredient { Name = "Pork", Amount = "500 g", Order = 1, UserName = owner });
        recipe.Steps.Add(new Step { Description = "Grill the meat.", Order = 1, UserName = owner });
        recipe.Tags = [new Tag { Name = "grill", UserName = owner }];
        ctx.Recipes.Add(recipe);
        return await ctx.SaveChangesAsync();
    });

    private async Task<RecipeDto> CloneAsync()
    {
        var response = await _client.PostAsync($"/api/recipes/{_guid}/clone", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RecipeDto>())!;
    }

    [Fact]
    public async Task Clone_OtherUsersRecipe_CreatesFullCopyInOwnCookbook()
    {
        await SeedGyrosAsync("otheruser");

        var clone = await CloneAsync();
        // Read back through the normal API, which filters every table by the current user.
        var fetched = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{clone.Guid}");

        Assert.NotEqual(_guid, fetched!.Guid);
        Assert.Equal("Gyros", fetched.Name);
        Assert.Equal("Greek", fetched.Category);
        Assert.Equal(4, fetched.Servings);
        Assert.Equal(["Pork"], fetched.Ingredients.Select(i => i.Name));
        Assert.Equal(["Grill the meat."], fetched.Steps.Select(s => s.Description));
        Assert.Equal(["grill"], fetched.Tags);
    }

    [Fact]
    public async Task Clone_OtherUsersRecipe_ChildrenBelongToCloner()
    {
        await SeedGyrosAsync("otheruser");

        var clone = await CloneAsync();

        var owners = await WithContextAsync(async ctx =>
        {
            var recipe = await ctx.Recipes
                .Include(r => r.Ingredients).Include(r => r.Steps).Include(r => r.Tags)
                .SingleAsync(r => r.Guid == clone.Guid);
            return recipe.Ingredients.Select(i => i.UserName)
                .Concat(recipe.Steps.Select(s => s.UserName))
                .Concat(recipe.Tags.Select(t => t.UserName))
                .Append(recipe.UserName)
                .ToList();
        });
        Assert.Equal(4, owners.Count);
        Assert.All(owners, owner => Assert.Equal("devuser", owner));
    }

    [Fact]
    public async Task Clone_OtherUsersRecipe_DoesNotCopyLastCooked()
    {
        await SeedGyrosAsync("otheruser");

        var clone = await CloneAsync();

        Assert.Null(clone.LastCooked);
    }

    [Fact]
    public async Task Clone_OtherUsersRecipe_LeavesOriginalUntouched()
    {
        await SeedGyrosAsync("otheruser");

        await CloneAsync();

        var original = await WithContextAsync(ctx => ctx.Recipes
            .Include(r => r.Ingredients).Include(r => r.Steps).Include(r => r.Tags)
            .SingleAsync(r => r.Guid == _guid));
        Assert.Equal("otheruser", original.UserName);
        Assert.Single(original.Ingredients);
        Assert.Single(original.Steps);
        Assert.Single(original.Tags);
        Assert.All(original.Ingredients, i => Assert.Equal("otheruser", i.UserName));
    }

    [Fact]
    public async Task Clone_OwnRecipe_AddsNumberedCopyAndKeepsLastCooked()
    {
        await SeedGyrosAsync("devuser");

        var clone = await CloneAsync();

        Assert.Equal("Gyros (1)", clone.Name);
        Assert.Equal(new DateTime(2026, 10, 1), clone.LastCooked);
        var fetched = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{clone.Guid}");
        Assert.Equal(["Pork"], fetched!.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public async Task Clone_UnknownRecipe_ReturnsNotFound()
    {
        var response = await _client.PostAsync($"/api/recipes/{Guid.NewGuid()}/clone", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
