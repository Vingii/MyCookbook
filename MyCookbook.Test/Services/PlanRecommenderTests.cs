using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Services;
using Plan = MyCookbook.Services.PlanRecommender.Plan;

namespace MyCookbook.Test.Services;

// MC-76: rule-based + learned plan suggestions.
public class PlanRecommenderTests
{
    private const string User = "devuser";
    private static readonly DateOnly Today = new(2026, 11, 15); // Sunday
    private int _nextId = 1;

    private Recipe R(string name, string category = "", int? duration = null, bool favorite = false,
        DateTime? lastCooked = null, params string[] tags)
    {
        var recipe = new Recipe
        {
            Id = _nextId++, Name = name, Category = category, Duration = duration, UserName = User,
            LastCooked = lastCooked, Guid = Guid.NewGuid(),
        };
        recipe.Tags = tags.Select(t => new Tag { Name = t, UserName = User }).ToList();
        recipe.FavoriteRecipes = favorite ? [new FavoriteRecipe { UserName = User, RecipeId = recipe.Id }] : [];
        return recipe;
    }

    private static List<string> Names(IEnumerable<MyCookbook.Api.Dto.PlanSuggestionDto> s) => s.Select(x => x.RecipeName).ToList();

    /// <summary>Plans one recipe per day for <paramref name="weeks"/> weeks before Today, chosen by <paramref name="pick"/>.</summary>
    private static List<Plan> History(int weeks, Func<DateOnly, int, Recipe> pick)
    {
        var plans = new List<Plan>();
        var start = Today.AddDays(-7 * weeks);
        for (var i = 0; i < 7 * weeks; i++)
        {
            var date = start.AddDays(i);
            plans.Add(new Plan(pick(date, i).Id, date, false));
        }
        return plans;
    }

    [Fact]
    public void NoHistory_PrefersFavoritesAndLongNotCooked()
    {
        var old = R("Old", lastCooked: new DateTime(2026, 5, 1));
        var recent = R("Recent", lastCooked: new DateTime(2026, 11, 1));
        var favorite = R("Favorite", favorite: true, lastCooked: new DateTime(2026, 8, 1));
        var never = R("Never");

        var result = PlanRecommender.Recommend([old, recent, favorite, never], [], User, Today.AddDays(1), Today, 4);

        Assert.Equal(["Favorite", "Old", "Never", "Recent"], Names(result));
        Assert.Contains(result[0].Reasons, r => r.Code == "favorite");
        Assert.Contains(result[1].Reasons, r => r.Code == "notCookedFor" && r.Days == 199);
        Assert.Contains(result[2].Reasons, r => r.Code == "neverCooked");
    }

    [Fact]
    public void ExcludesRecipesPlannedWithinAWeek_IncludingFromFridge()
    {
        var a = R("A");
        var b = R("B");
        var c = R("C");
        var d = R("D");
        var target = Today.AddDays(3);
        List<Plan> plans =
        [
            new(a.Id, target.AddDays(5), false),
            new(b.Id, target.AddDays(-7), true),
            new(c.Id, target.AddDays(8), false),
        ];

        var result = PlanRecommender.Recommend([a, b, c, d], plans, User, target, Today, 10);

        Assert.Equal(["C", "D"], Names(result).OrderBy(n => n));
    }

    [Fact]
    public void LearnsCategoryOnAWeekday()
    {
        var soups = Enumerable.Range(1, 3).Select(i => R($"Soup {i}", "Polévky")).ToList();
        var mains = Enumerable.Range(1, 10).Select(i => R($"Main {i}", "Hlavní jídla")).ToList();
        // Every Sunday a soup, every other day a main dish.
        var plans = History(30, (date, i) => date.DayOfWeek == DayOfWeek.Sunday ? soups[i / 7 % 3] : mains[i % 10]);
        List<Recipe> all = [.. soups, .. mains];

        var sunday = PlanRecommender.Recommend(all, plans, User, Today.AddDays(7), Today, 3);
        var wednesday = PlanRecommender.Recommend(all, plans, User, Today.AddDays(3), Today, 3);

        Assert.StartsWith("Soup", sunday[0].RecipeName);
        var reason = sunday[0].Reasons[0];
        Assert.Equal("weekdayCategory", reason.Code);
        Assert.Equal(0, reason.Weekday);
        Assert.Equal("Polévky", reason.Category);
        Assert.StartsWith("Main", wednesday[0].RecipeName);
    }

    [Fact]
    public void LearnsQuickOnWeekdaysAndLongOnWeekends()
    {
        var quick = Enumerable.Range(1, 12).Select(i => R($"Quick {i}", $"Q{i}", duration: 20)).ToList();
        var slow = Enumerable.Range(1, 6).Select(i => R($"Slow {i}", $"S{i}", duration: 180)).ToList();
        var plans = History(30, (date, i) =>
            date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? slow[i % 6] : quick[i % 12]);
        List<Recipe> all = [.. quick, .. slow];

        var saturday = PlanRecommender.Recommend(all, plans, User, Today.AddDays(6), Today, 6);
        var tuesday = PlanRecommender.Recommend(all, plans, User, Today.AddDays(2), Today, 6);

        Assert.All(saturday.Take(3), s => Assert.StartsWith("Slow", s.RecipeName));
        Assert.All(tuesday.Take(3), s => Assert.StartsWith("Quick", s.RecipeName));
        Assert.Contains(saturday[0].Reasons, r => r.Code == "weekendDuration" && r.Duration == "long");
    }

    [Fact]
    public void KeepsSuggestionsVaried()
    {
        var recipes = Enumerable.Range(1, 5).Select(i => R($"Soup {i}", "Polévky", favorite: true))
            .Append(R("Salad", "Saláty")).ToList();

        var result = PlanRecommender.Recommend(recipes, [], User, Today, Today, 3);

        Assert.Equal(2, result.Count(r => r.Category == "Polévky"));
        Assert.Contains(result, r => r.RecipeName == "Salad");
    }

    [Fact]
    public void TopsUpFromOneCategoryWhenNothingElseIsLeft()
    {
        var recipes = Enumerable.Range(1, 4).Select(i => R($"Soup {i}", "Polévky")).ToList();
        Assert.Equal(4, PlanRecommender.Recommend(recipes, [], User, Today, Today, 6).Count);
    }

    [Fact]
    public void IsDeterministic()
    {
        var recipes = Enumerable.Range(1, 12).Select(i => R($"R{i}", $"C{i % 4}", duration: 15 * i)).ToList();
        var plans = History(10, (_, i) => recipes[i * 5 % 12]);

        var first = PlanRecommender.Recommend(recipes, plans, User, Today.AddDays(2), Today, 5);
        var second = PlanRecommender.Recommend(recipes, plans, User, Today.AddDays(2), Today, 5);

        Assert.Equal(first.Select(s => (s.RecipeGuid, s.Score)), second.Select(s => (s.RecipeGuid, s.Score)));
    }

    [Fact]
    public void EmptyCookbook_ReturnsNothing()
    {
        Assert.Empty(PlanRecommender.Recommend([], [], User, Today, Today, 5));
    }
}
