using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// MC-71: history, pinning and lookup of recipes shared with you.
// Authenticates as "devuser" — see the note on RecipesControllerTests.
public class SharedApiTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    private readonly Guid _gyros = Guid.NewGuid();
    private readonly Guid _tzatziki = Guid.NewGuid();
    private readonly Guid _hummus = Guid.NewGuid();
    private readonly Guid _ownRecipe = Guid.NewGuid();

    public SharedApiTests()
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

    /// <summary>"anna" (display name "Anna Nováková") owns Gyros, Tzatziki and Hummus.</summary>
    private Task SeedAsync() => WithContextAsync(ctx =>
    {
        ctx.Recipes.Add(new Recipe { Guid = _gyros, Name = "Gyros", UserName = "anna" });
        ctx.Recipes.Add(new Recipe { Guid = _tzatziki, Name = "Tzatziki", UserName = "anna" });
        ctx.Recipes.Add(new Recipe { Guid = _hummus, Name = "Hummus", UserName = "anna" });
        ctx.Recipes.Add(new Recipe { Guid = _ownRecipe, Name = "My soup", UserName = "devuser" });
        ctx.UserPreferences.Add(new UserPreference { UserName = "anna", Key = "DisplayName", Value = "Anna Nováková" });
        return Task.CompletedTask;
    });

    private async Task<List<SharedItemDto>> ListAsync() =>
        (await _client.GetFromJsonAsync<List<SharedItemDto>>("/api/shared"))!;

    private Task<HttpResponseMessage> OpenRecipeAsync(Guid guid) =>
        _client.PostAsync($"/api/shared/recipes/{guid}", null);

    [Fact]
    public async Task OpeningSharedRecipe_AddsItToHistory()
    {
        await SeedAsync();

        var response = await OpenRecipeAsync(_gyros);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var item = Assert.Single(await ListAsync());
        Assert.Equal(_gyros, item.RecipeGuid);
        Assert.Equal("Gyros", item.RecipeName);
        Assert.Equal("Anna Nováková", item.OwnerName);
        Assert.True(item.Available);
        Assert.False(item.Pinned);
    }

    [Fact]
    public async Task OpeningSameRecipeTwice_KeepsOneEntryAndUpdatesLastOpened()
    {
        await SeedAsync();
        await OpenRecipeAsync(_gyros);
        var first = Assert.Single(await ListAsync()).LastOpened;

        await Task.Delay(20);
        await OpenRecipeAsync(_gyros);

        var item = Assert.Single(await ListAsync());
        Assert.True(item.LastOpened > first);
    }

    [Fact]
    public async Task OpeningOwnRecipe_IsNotRecorded()
    {
        await SeedAsync();

        var response = await OpenRecipeAsync(_ownRecipe);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task OpeningUnknownRecipe_ReturnsNotFound()
    {
        var response = await OpenRecipeAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task DeletedRecipe_StaysInHistoryAsUnavailable()
    {
        await SeedAsync();
        await OpenRecipeAsync(_gyros);

        await WithContextAsync(async ctx => ctx.Recipes.Remove(await ctx.Recipes.SingleAsync(r => r.Guid == _gyros)));

        var item = Assert.Single(await ListAsync());
        Assert.False(item.Available);
        Assert.Null(item.RecipeName);
    }

    [Fact]
    public async Task RenamedRecipe_ShowsCurrentName()
    {
        await SeedAsync();
        await OpenRecipeAsync(_gyros);

        await WithContextAsync(async ctx => (await ctx.Recipes.SingleAsync(r => r.Guid == _gyros)).Name = "Chicken gyros");

        Assert.Equal("Chicken gyros", Assert.Single(await ListAsync()).RecipeName);
    }

    [Fact]
    public async Task History_IsPinnedFirstThenMostRecent()
    {
        await SeedAsync();
        await OpenRecipeAsync(_gyros);
        await Task.Delay(20);
        await OpenRecipeAsync(_tzatziki);
        await Task.Delay(20);
        await OpenRecipeAsync(_hummus);
        Assert.Equal([_hummus, _tzatziki, _gyros], (await ListAsync()).Select(i => i.RecipeGuid));

        var gyrosId = (await ListAsync()).Single(i => i.RecipeGuid == _gyros).Id;
        var pin = await _client.PutAsJsonAsync($"/api/shared/{gyrosId}/pin", new { pinned = true });

        Assert.Equal(HttpStatusCode.NoContent, pin.StatusCode);
        var items = await ListAsync();
        Assert.Equal([_gyros, _hummus, _tzatziki], items.Select(i => i.RecipeGuid));
        Assert.True(items[0].Pinned);

        await _client.PutAsJsonAsync($"/api/shared/{gyrosId}/pin", new { pinned = false });
        Assert.All(await ListAsync(), i => Assert.False(i.Pinned));
    }

    [Fact]
    public async Task Remove_DeletesEntryFromHistory()
    {
        await SeedAsync();
        await OpenRecipeAsync(_gyros);
        var id = Assert.Single(await ListAsync()).Id;

        var response = await _client.DeleteAsync($"/api/shared/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task OtherUsersHistory_IsInvisibleAndUntouchable()
    {
        await SeedAsync();
        var othersItem = new SharedItem
        {
            UserName = "anna", OwnerUserName = "devuser",
            RecipeGuid = _ownRecipe, LastOpened = DateTime.UtcNow,
        };
        await WithContextAsync(ctx => { ctx.SharedItems.Add(othersItem); return Task.CompletedTask; });

        Assert.Empty(await ListAsync());
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutAsJsonAsync($"/api/shared/{othersItem.Id}/pin", new { pinned = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/shared/{othersItem.Id}")).StatusCode);
    }
}
