using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Services;

/// <summary>
/// Suggests recipes to plan for a day. Rules give a sensible starting point (recipes not cooked for a long
/// time, favorites; nothing planned within a week of the day). On top of that a small logistic regression is
/// trained on the user's own plan history to pick up their soft rules: categories or tags they cook on certain
/// weekdays, quick vs. long recipes on weekdays vs. weekends, avoiding the same category two days in a row,
/// recipes they come back to. The more history, the more the learned model counts.
/// </summary>
public static class PlanRecommender
{
    /// <summary>Recipes planned this many days before or after the target day are not suggested.</summary>
    public const int ExclusionDays = 7;
    /// <summary>Most suggestions from one category, so the list stays varied.</summary>
    public const int MaxPerCategory = 2;
    private const int NegativesPerPositive = 4;
    private const int Epochs = 25;
    private const double LearningRate = 0.1;
    private const double L2 = 1e-4;
    /// <summary>History size at which the learned model and the rules count equally.</summary>
    private const double HalfTrustPositives = 20;

    public record Plan(int RecipeId, DateOnly Date, bool FromFridge);

    public static List<PlanSuggestionDto> Recommend(
        IReadOnlyList<Recipe> recipes, IReadOnlyList<Plan> plans, string user, DateOnly date, DateOnly today, int count)
    {
        if (recipes.Count == 0 || count <= 0) return [];
        var history = new History(recipes, plans, user);

        // Hard rule: nothing that is planned (or being eaten from the fridge) around that day.
        var blocked = plans
            .Where(p => Math.Abs(p.Date.DayNumber - date.DayNumber) <= ExclusionDays)
            .Select(p => p.RecipeId).ToHashSet();
        var candidates = recipes.Where(r => !blocked.Contains(r.Id)).ToList();
        if (candidates.Count == 0) return [];

        var model = Train(history, today);
        var trust = model.Positives / (model.Positives + HalfTrustPositives);

        var scored = candidates.Select(r =>
        {
            var features = history.Features(r, date);
            return (Recipe: r, Features: features, P: model.Predict(features), Prior: history.Prior(r, date));
        }).ToList();
        var maxP = scored.Max(s => s.P);

        var ranked = scored
            .Select(s => (s.Recipe, s.Features, Score: trust * (maxP > 0 ? s.P / maxP : 0) + (1 - trust) * s.Prior))
            .OrderByDescending(s => s.Score).ThenBy(s => s.Recipe.Name, StringComparer.Ordinal)
            .ToList();

        // Keep the list varied: at most MaxPerCategory per category, topped up from the rest if needed.
        var picked = new List<(Recipe Recipe, Dictionary<string, double> Features, double Score)>();
        var perCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<(Recipe Recipe, Dictionary<string, double> Features, double Score)>();
        foreach (var s in ranked)
        {
            if (picked.Count == count) break;
            var category = s.Recipe.Category?.Trim() ?? "";
            if (category.Length > 0 && perCategory.GetValueOrDefault(category) >= MaxPerCategory)
            {
                skipped.Add(s);
                continue;
            }
            if (category.Length > 0) perCategory[category] = perCategory.GetValueOrDefault(category) + 1;
            picked.Add(s);
        }
        picked.AddRange(skipped.Take(count - picked.Count));

        return picked.Select(s => new PlanSuggestionDto
        {
            RecipeGuid = s.Recipe.Guid,
            RecipeName = s.Recipe.Name,
            Category = s.Recipe.Category ?? "",
            Score = Math.Round(s.Score, 4),
            Reasons = Reasons(s.Recipe, s.Features, model, trust, history, date),
        }).ToList();
    }

    private static Model Train(History history, DateOnly today)
    {
        var positives = history.CookedPlans.Where(p => p.Date < today).ToList();
        var model = new Model { Positives = positives.Count };
        if (positives.Count == 0 || history.Recipes.Count < 2) return model;

        // Deterministic so suggestions don't change between page loads.
        var random = new Random(positives.Count * 7919 + history.Recipes.Count);
        var examples = new List<(Dictionary<string, double> X, double Y)>();
        foreach (var plan in positives)
        {
            if (!history.ById.TryGetValue(plan.RecipeId, out var recipe)) continue;
            examples.Add((history.Features(recipe, plan.Date), 1));
            for (var i = 0; i < NegativesPerPositive; i++)
            {
                var other = history.Recipes[random.Next(history.Recipes.Count)];
                if (other.Id != recipe.Id) examples.Add((history.Features(other, plan.Date), 0));
            }
        }

        for (var epoch = 0; epoch < Epochs; epoch++)
        {
            var rate = LearningRate / (1 + epoch * 0.2);
            foreach (var i in Enumerable.Range(0, examples.Count).OrderBy(_ => random.Next()))
            {
                var (x, y) = examples[i];
                var error = model.Predict(x) - y;
                foreach (var (name, value) in x)
                {
                    var w = model.Weights.GetValueOrDefault(name);
                    model.Weights[name] = w - rate * (error * value + L2 * w);
                }
            }
        }
        return model;
    }

    private static List<PlanSuggestionReasonDto> Reasons(
        Recipe recipe, Dictionary<string, double> features, Model model, double trust, History history, DateOnly date)
    {
        var reasons = new List<PlanSuggestionReasonDto>();

        // What the model learned, if it has enough history to be trusted: the strongest preference that applies.
        if (trust >= 0.3)
        {
            var learned = features
                .Where(f => !f.Key.StartsWith('_'))
                .Select(f => (f.Key, Contribution: model.Weights.GetValueOrDefault(f.Key) * f.Value))
                .Where(f => f.Contribution > 0.4)
                .OrderByDescending(f => f.Contribution)
                .Select(f => LearnedReason(f.Key, recipe))
                .FirstOrDefault(r => r != null);
            if (learned != null) reasons.Add(learned);
        }

        var daysSince = history.DaysSinceCooked(recipe, date);
        if (daysSince == null)
            reasons.Add(new PlanSuggestionReasonDto { Code = "neverCooked" });
        else if (daysSince >= 21)
            reasons.Add(new PlanSuggestionReasonDto { Code = "notCookedFor", Days = daysSince });
        if (history.IsFavorite(recipe))
            reasons.Add(new PlanSuggestionReasonDto { Code = "favorite" });

        return reasons.Take(2).ToList();
    }

    private static PlanSuggestionReasonDto? LearnedReason(string feature, Recipe recipe)
    {
        var parts = feature.Split('|').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");
        int? weekday = parts.TryGetValue("dow", out var d) ? int.Parse(d) : null;
        if (weekday != null && parts.TryGetValue("cat", out var cat))
            return new() { Code = "weekdayCategory", Weekday = weekday, Category = DisplayCategory(recipe, cat) };
        if (weekday != null && parts.TryGetValue("tag", out var tag))
            return new() { Code = "weekdayTag", Weekday = weekday, Tag = DisplayTag(recipe, tag) };
        if (parts.TryGetValue("weekend", out var weekend) && parts.TryGetValue("dur", out var dur) && dur != "unknown")
            return new() { Code = weekend == "1" ? "weekendDuration" : "weekdayDuration", Duration = dur };
        if (parts.ContainsKey("recipe"))
            return new() { Code = "often" };
        return null;
    }

    private static string DisplayCategory(Recipe recipe, string normalized) =>
        recipe.Category?.Trim() is { Length: > 0 } c && Normalize(c) == normalized ? c : normalized;

    private static string DisplayTag(Recipe recipe, string normalized) =>
        recipe.Tags?.Select(t => t.Name.Trim()).FirstOrDefault(t => Normalize(t) == normalized) ?? normalized;

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();

    private static double Sigmoid(double z) => 1 / (1 + Math.Exp(-Math.Clamp(z, -30, 30)));

    private sealed class Model
    {
        public Dictionary<string, double> Weights { get; } = new();
        public int Positives { get; init; }

        public double Predict(Dictionary<string, double> x) =>
            Sigmoid(x.Sum(f => Weights.GetValueOrDefault(f.Key) * f.Value));
    }

    /// <summary>The user's recipes and plans, with the features describing "recipe R planned on day D".</summary>
    private sealed class History
    {
        public List<Recipe> Recipes { get; }
        public Dictionary<int, Recipe> ById { get; }
        /// <summary>Plans that were cooking decisions (not eaten from the fridge), by date.</summary>
        public List<Plan> CookedPlans { get; }
        private readonly Dictionary<int, List<DateOnly>> _datesByRecipe;
        private readonly Dictionary<DateOnly, List<int>> _recipesByDate;
        private readonly string _user;

        public History(IReadOnlyList<Recipe> recipes, IReadOnlyList<Plan> plans, string user)
        {
            _user = user;
            Recipes = recipes.ToList();
            ById = Recipes.ToDictionary(r => r.Id);
            CookedPlans = plans.Where(p => !p.FromFridge && ById.ContainsKey(p.RecipeId)).OrderBy(p => p.Date).ToList();
            _datesByRecipe = CookedPlans.GroupBy(p => p.RecipeId).ToDictionary(g => g.Key, g => g.Select(p => p.Date).ToList());
            _recipesByDate = CookedPlans.GroupBy(p => p.Date).ToDictionary(g => g.Key, g => g.Select(p => p.RecipeId).ToList());
        }

        public bool IsFavorite(Recipe recipe) => recipe.IsFavorite(_user);

        /// <summary>Days since the recipe was last cooked before <paramref name="date"/>, or null if never.</summary>
        public int? DaysSinceCooked(Recipe recipe, DateOnly date)
        {
            DateOnly? last = _datesByRecipe.TryGetValue(recipe.Id, out var dates)
                ? dates.Where(d => d < date).Select(d => (DateOnly?)d).LastOrDefault()
                : null;
            if (recipe.LastCooked is { } lc && DateOnly.FromDateTime(lc) is var lcDate && lcDate < date && (last == null || lcDate > last))
                last = lcDate;
            return last == null ? null : date.DayNumber - last.Value.DayNumber;
        }

        private int TimesCooked(Recipe recipe, DateOnly date, int withinDays) =>
            _datesByRecipe.TryGetValue(recipe.Id, out var dates)
                ? dates.Count(d => d < date && date.DayNumber - d.DayNumber <= withinDays)
                : 0;

        /// <summary>Rule-based score in [0, 1]: long not cooked and favorites first.</summary>
        public double Prior(Recipe recipe, DateOnly date)
        {
            var days = DaysSinceCooked(recipe, date);
            var recency = days == null ? 0.7 : Math.Min(days.Value, 120) / 120.0;
            var favorite = IsFavorite(recipe) ? 1 : 0;
            return 0.65 * recency + 0.35 * favorite;
        }

        /// <summary>
        /// Features of planning <paramref name="recipe"/> on <paramref name="date"/>, computed only from what was
        /// planned before that day. Names starting with "_" are numeric context; the rest are learned preferences.
        /// </summary>
        public Dictionary<string, double> Features(Recipe recipe, DateOnly date)
        {
            var dow = (int)date.DayOfWeek;
            var weekend = dow is 0 or 6 ? 1 : 0;
            var category = Normalize(recipe.Category ?? "");
            var tags = (recipe.Tags ?? []).Select(t => Normalize(t.Name)).Where(t => t.Length > 0).Distinct().ToList();
            var duration = recipe.Duration switch
            {
                null or <= 0 => "unknown",
                <= 30 => "quick",
                <= 60 => "medium",
                _ => "long",
            };
            var days = DaysSinceCooked(recipe, date);

            var x = new Dictionary<string, double>
            {
                ["_bias"] = 1,
                ["_recency"] = days == null ? 0 : Math.Min(days.Value, 365) / 365.0,
                ["_never"] = days == null ? 1 : 0,
                ["_recent"] = Math.Min(TimesCooked(recipe, date, 90), 6) / 6.0,
                ["_favorite"] = IsFavorite(recipe) ? 1 : 0,
                [$"recipe={recipe.Id}"] = 1,
                [$"weekend={weekend}|dur={duration}"] = 1,
            };
            if (category.Length > 0)
            {
                x[$"cat={category}"] = 1;
                x[$"dow={dow}|cat={category}"] = 1;
                var yesterday = date.AddDays(-1);
                var sameAsYesterday = _recipesByDate.TryGetValue(yesterday, out var ids)
                    && ids.Any(id => ById.TryGetValue(id, out var r) && Normalize(r.Category ?? "") == category);
                x["_sameCategoryAsYesterday"] = sameAsYesterday ? 1 : 0;
            }
            foreach (var tag in tags)
            {
                x[$"tag={tag}"] = 1;
                x[$"dow={dow}|tag={tag}"] = 1;
            }
            return x;
        }
    }
}
