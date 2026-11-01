using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Services;

namespace MyCookbook.Test.Services;

public class DevDataSeederTests
{
    private const string DevUser = "devuser";
    private static readonly DateOnly Today = new(2026, 11, 1);

    private readonly DbContextOptions<CookbookDatabaseContext> _options = new DbContextOptionsBuilder<CookbookDatabaseContext>()
        .UseInMemoryDatabase($"DevDataSeederTests{Guid.NewGuid()}")
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    private CookbookDatabaseContext NewContext() => new(_options);

    private async Task SeedAsync()
    {
        await using var context = NewContext();
        await DevDataSeeder.SeedAsync(context, DevUser, Today);
    }

    private async Task<List<Recipe>> RecipesOfAsync(string user)
    {
        await using var context = NewContext();
        return await context.Recipes.Where(r => r.UserName == user)
            .Include(r => r.Ingredients).Include(r => r.Steps).Include(r => r.Tags)
            .ToListAsync();
    }

    [Fact]
    public async Task SeedsDevUserCookbookWithFullRecipes()
    {
        await SeedAsync();

        var recipes = await RecipesOfAsync(DevUser);
        Assert.Equal(8, recipes.Count);
        Assert.All(recipes, r =>
        {
            Assert.NotEmpty(r.Ingredients);
            Assert.NotEmpty(r.Steps);
            Assert.NotEmpty(r.Tags);
            // Every table is filtered per user, so children must belong to the same user.
            Assert.All(r.Ingredients, i => Assert.Equal(DevUser, i.UserName));
            Assert.All(r.Steps, s => Assert.Equal(DevUser, s.UserName));
            Assert.All(r.Tags, t => Assert.Equal(DevUser, t.UserName));
        });
        Assert.Contains(recipes.SelectMany(r => r.Steps), s => s.Description.Contains("[[Tzatziki]]"));
    }

    [Fact]
    public async Task SeedsFavoritesLastCookedAndAPlanAroundToday()
    {
        await SeedAsync();

        await using var context = NewContext();
        Assert.Equal(2, await context.FavoriteRecipes.CountAsync(f => f.UserName == DevUser));
        var plan = await context.PlannedRecipes.Where(p => p.UserName == DevUser).Select(p => p.Date).ToListAsync();
        Assert.Contains(Today, plan);
        Assert.Contains(plan, d => d < Today);
        Assert.Contains(plan, d => d > Today);
        Assert.Equal(2, await context.Recipes.CountAsync(r => r.UserName == DevUser && r.LastCooked != null));
    }

    [Fact]
    public async Task SeedsSharingUserWithDisplayNameAndShareLink()
    {
        await SeedAsync();

        Assert.Equal(3, (await RecipesOfAsync(DevDataSeeder.SharingUser)).Count);
        await using var context = NewContext();
        var prefs = await context.UserPreferences.Where(p => p.UserName == DevDataSeeder.SharingUser)
            .ToDictionaryAsync(p => p.Key, p => p.Value);
        Assert.Equal(DevDataSeeder.SharingUserDisplayName, prefs["DisplayName"]);
        Assert.Equal(DevDataSeeder.SharingUserToken, prefs["ShareToken"]);
    }

    [Fact]
    public async Task RunningTwice_DoesNotDuplicate()
    {
        await SeedAsync();
        await SeedAsync();

        Assert.Equal(8, (await RecipesOfAsync(DevUser)).Count);
        Assert.Equal(3, (await RecipesOfAsync(DevDataSeeder.SharingUser)).Count);
        await using var context = NewContext();
        Assert.Equal(5, await context.PlannedRecipes.CountAsync(p => p.UserName == DevUser));
    }

    [Fact]
    public async Task DeletedRecipes_DoNotComeBackOnRestart()
    {
        await SeedAsync();
        await using (var context = NewContext())
        {
            context.Recipes.Remove(await context.Recipes.FirstAsync(r => r.UserName == DevUser && r.Name == "Palačinky"));
            await context.SaveChangesAsync();
        }

        await SeedAsync();

        Assert.DoesNotContain(await RecipesOfAsync(DevUser), r => r.Name == "Palačinky");
    }

    [Fact]
    public async Task ExistingData_IsKept_AndClashingNamesSkipped()
    {
        await using (var context = NewContext())
        {
            context.Recipes.Add(new Recipe { UserName = DevUser, Name = "test", Servings = 1 });
            context.Recipes.Add(new Recipe { UserName = DevUser, Name = "Palačinky", Servings = 2 });
            context.UserPreferences.Add(new UserPreference { UserName = DevDataSeeder.SharingUser, Key = "ShareToken", Value = "existing" });
            await context.SaveChangesAsync();
        }

        await SeedAsync();

        var recipes = await RecipesOfAsync(DevUser);
        Assert.Equal(9, recipes.Count); // 8 seeded, minus the clashing name, plus the 2 existing
        Assert.Single(recipes, r => r.Name == "Palačinky");
        Assert.Empty(recipes.Single(r => r.Name == "Palačinky").Ingredients); // the user's own, untouched
        await using var check = NewContext();
        Assert.Equal("existing", (await check.UserPreferences.SingleAsync(p => p.UserName == DevDataSeeder.SharingUser && p.Key == "ShareToken")).Value);
    }
}
