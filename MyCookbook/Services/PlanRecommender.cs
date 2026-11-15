using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Services;

/// <summary>
/// Suggests recipes to plan in the coming days. Rules give a sensible starting point (recipes not cooked for a
/// long time, favorites; nothing planned within a week of the period). On top of that a small logistic regression
/// is trained on the user's own plan history to pick up their soft rules: categories or tags they cook on certain
/// weekdays or in certain seasons, quick vs. long recipes on weekdays vs. weekends, avoiding the same category two
/// days in a row, recipes they come back to. Recent plans count more than old ones, and the more history, the more
/// the learned model counts. Side dishes (recipes usually planned alongside another one) are not suggested on
/// their own but attached to the main they go with.
/// </summary>
public static class PlanRecommender
{
    /// <summary>Recipes planned this many days before or after the period are not suggested.</summary>
    public const int ExclusionDays = 7;
    /// <summary>Most suggestions from one category, so the list stays varied.</summary>
    public const int MaxPerCategory = 2;
    /// <summary>Only this much history is learned from.</summary>
    public const int HistoryDays = 3 * 365;
    /// <summary>A plan's weight halves with every this many days of age, so current habits dominate.</summary>
    public const double HalfLifeDays = 365;
    private const int NegativesPerPositive = 4;
    private const int Epochs = 25;
    private const double LearningRate = 0.1;
    private const double L2 = 1e-4;
    /// <summary>Weighted history size at which the learned model and the rules count equally.</summary>
    private const double HalfTrustPositives = 20;
    /// <summary>A recipe is a side dish if at least this share of its days has another recipe planned too…</summary>
    private const double SideShare = 0.75;
    /// <summary>…and a side is attached to a main it shares at least this share of the main's days with.</summary>
    private const double CompanionShare = 0.4;
    private const int MaxCompanions = 2;

    public record Plan(int RecipeId, DateOnly Date, bool FromFridge);

    /// <summary>Learn from the history and suggest in one go. See <see cref="Train"/> and <see cref="Suggest"/>.</summary>
    public static List<PlanSuggestionDto> Recommend(
        IReadOnlyList<Recipe> recipes, IReadOnlyList<Plan> plans, string user,
        DateOnly from, DateOnly to, DateOnly today, int count) =>
        Suggest(Train(recipes, plans, user, today), plans, from, to, count);

    /// <summary>Learns the user's preferences. The result can be cached until the recipes or plans change.</summary>
    public static TrainedModel Train(IReadOnlyList<Recipe> recipes, IReadOnlyList<Plan> plans, string user, DateOnly today)
    {
        var history = new History(recipes, plans, user, today);
        var model = new Model();
        var positives = history.CookedPlans
            .Where(p => p.Date < today && today.DayNumber - p.Date.DayNumber <= HistoryDays)
            .ToList();
        var effective = positives.Sum(p => history.Weight(p.Date));

        if (positives.Count > 0 && history.Recipes.Count >= 2)
        {
            // Deterministic so suggestions don't change between page loads.
            var random = new Random(positives.Count * 7919 + history.Recipes.Count);
            // Features are numbered once, so training works on arrays instead of hashing names on every update.
            var vocabulary = new Dictionary<string, int>();
            var examples = new List<(int[] Index, double[] Value, double Y, double W)>();
            void Add(Dictionary<string, double> features, double y, double w)
            {
                var index = new int[features.Count];
                var value = new double[features.Count];
                var k = 0;
                foreach (var (name, v) in features)
                {
                    if (!vocabulary.TryGetValue(name, out var id)) vocabulary[name] = id = vocabulary.Count;
                    index[k] = id;
                    value[k++] = v;
                }
                examples.Add((index, value, y, w));
            }
            foreach (var plan in positives)
            {
                if (!history.ById.TryGetValue(plan.RecipeId, out var recipe)) continue;
                var weight = history.Weight(plan.Date);
                Add(history.Features(recipe, plan.Date), 1, weight);
                for (var i = 0; i < NegativesPerPositive; i++)
                {
                    var other = history.Recipes[random.Next(history.Recipes.Count)];
                    if (other.Id != recipe.Id) Add(history.Features(other, plan.Date), 0, weight);
                }
            }

            var weights = new double[vocabulary.Count];
            var order = Enumerable.Range(0, examples.Count).ToArray();
            for (var epoch = 0; epoch < Epochs; epoch++)
            {
                random.Shuffle(order);
                var rate = LearningRate / (1 + epoch * 0.2);
                foreach (var i in order)
                {
                    var (index, value, y, w) = examples[i];
                    var z = 0.0;
                    for (var k = 0; k < index.Length; k++) z += weights[index[k]] * value[k];
                    var error = (Sigmoid(z) - y) * w;
                    for (var k = 0; k < index.Length; k++)
                        weights[index[k]] -= rate * (error * value[k] + L2 * weights[index[k]]);
                }
            }
            foreach (var (name, id) in vocabulary) model.Weights[name] = weights[id];
        }

        return new TrainedModel(history, model, effective / (effective + HalfTrustPositives));
    }

    /// <summary>
    /// The best <paramref name="count"/> recipes to plan between <paramref name="from"/> and <paramref name="to"/>.
    /// A recipe's score is its best score on any day of the period, so recipes tied to a weekday still come up.
    /// </summary>
    public static List<PlanSuggestionDto> Suggest(TrainedModel trained, IReadOnlyList<Plan> plans, DateOnly from, DateOnly to, int count)
    {
        var (history, model, trust) = (trained.History, trained.Model, trained.Trust);
        if (history.Recipes.Count == 0 || count <= 0 || to < from) return [];

        // Hard rule: nothing that is planned (or being eaten from the fridge) around the period.
        var blocked = plans
            .Where(p => p.Date.DayNumber >= from.DayNumber - ExclusionDays && p.Date.DayNumber <= to.DayNumber + ExclusionDays)
            .Select(p => p.RecipeId).ToHashSet();
        // Side dishes come with their main instead of on their own.
        var candidates = history.Recipes.Where(r => !blocked.Contains(r.Id) && !history.IsSide(r.Id)).ToList();
        if (candidates.Count == 0) return [];

        var dates = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays).ToList();
        var perDay = candidates.SelectMany(r => dates.Select(d =>
        {
            var features = history.Features(r, d);
            return (Recipe: r, Date: d, Features: features, P: model.Predict(features), Prior: history.Prior(r, d));
        })).ToList();
        var maxP = perDay.Max(s => s.P);

        var ranked = perDay
            .Select(s => (s.Recipe, s.Date, s.Features, Score: trust * (maxP > 0 ? s.P / maxP : 0) + (1 - trust) * s.Prior))
            .GroupBy(s => s.Recipe.Id)
            .Select(g => g.MaxBy(s => s.Score))
            .OrderByDescending(s => s.Score).ThenBy(s => s.Recipe.Name, StringComparer.Ordinal)
            .ToList();

        // Keep the list varied: at most MaxPerCategory per category, topped up from the rest if needed.
        var picked = new List<(Recipe Recipe, DateOnly Date, Dictionary<string, double> Features, double Score)>();
        var skipped = new List<(Recipe Recipe, DateOnly Date, Dictionary<string, double> Features, double Score)>();
        var perCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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
            Companions = history.Companions(s.Recipe.Id)
                .Select(c => new PlanSuggestionCompanionDto { RecipeGuid = c.Guid, RecipeName = c.Name })
                .ToList(),
            Reasons = Reasons(s.Recipe, s.Features, model, trust, history, s.Date),
        }).ToList();
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
        parts.TryGetValue("season", out var season);
        if (parts.TryGetValue("cat", out var cat) && (weekday != null || season != null))
            return new() { Code = weekday != null ? "weekdayCategory" : "seasonCategory", Weekday = weekday, Season = season, Category = DisplayCategory(recipe, cat) };
        if (parts.TryGetValue("tag", out var tag) && (weekday != null || season != null))
            return new() { Code = weekday != null ? "weekdayTag" : "seasonTag", Weekday = weekday, Season = season, Tag = DisplayTag(recipe, tag) };
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

    /// <summary>Meteorological season of the northern hemisphere.</summary>
    public static string Season(DateOnly date) => date.Month switch
    {
        12 or 1 or 2 => "winter",
        3 or 4 or 5 => "spring",
        6 or 7 or 8 => "summer",
        _ => "autumn",
    };

    private static double Sigmoid(double z) => 1 / (1 + Math.Exp(-Math.Clamp(z, -30, 30)));

    /// <summary>A user's learned preferences, valid for the recipes and plans it was trained on.</summary>
    public sealed record TrainedModel(History History, Model Model, double Trust);

    public sealed class Model
    {
        public Dictionary<string, double> Weights { get; } = new();

        public double Predict(Dictionary<string, double> x)
        {
            var z = 0.0;
            foreach (var (name, value) in x) z += Weights.GetValueOrDefault(name) * value;
            return Sigmoid(z);
        }
    }

    /// <summary>The user's recipes and plans, with the features describing "recipe R planned on day D".</summary>
    public sealed class History
    {
        public List<Recipe> Recipes { get; }
        public Dictionary<int, Recipe> ById { get; }
        /// <summary>Plans that were cooking decisions (not eaten from the fridge), by date.</summary>
        public List<Plan> CookedPlans { get; }
        private readonly Dictionary<int, DateOnly[]> _datesByRecipe;
        private readonly Dictionary<DateOnly, List<int>> _recipesByDate;
        private readonly HashSet<int> _sides = [];
        private readonly Dictionary<int, List<Recipe>> _companions = new();
        private readonly string _user;
        private readonly DateOnly _today;

        public History(IReadOnlyList<Recipe> recipes, IReadOnlyList<Plan> plans, string user, DateOnly today)
        {
            _user = user;
            _today = today;
            Recipes = recipes.ToList();
            ById = Recipes.ToDictionary(r => r.Id);
            CookedPlans = plans.Where(p => !p.FromFridge && ById.ContainsKey(p.RecipeId)).OrderBy(p => p.Date).ToList();
            // Sorted per recipe, so "last cooked before D" and "times cooked recently" are binary searches.
            _datesByRecipe = CookedPlans.GroupBy(p => p.RecipeId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.Date).Distinct().OrderBy(d => d).ToArray());
            _recipesByDate = CookedPlans.GroupBy(p => p.Date)
                .ToDictionary(g => g.Key, g => g.Select(p => p.RecipeId).Distinct().ToList());
            FindSideDishes();
        }

        /// <summary>How much a plan on <paramref name="date"/> counts: halves every <see cref="HalfLifeDays"/>.</summary>
        public double Weight(DateOnly date) => Math.Pow(0.5, Math.Max(0, _today.DayNumber - date.DayNumber) / HalfLifeDays);

        public bool IsFavorite(Recipe recipe) => recipe.IsFavorite(_user);

        public bool IsSide(int recipeId) => _sides.Contains(recipeId);

        /// <summary>Side dishes usually planned with this recipe, most frequent first.</summary>
        public IReadOnlyList<Recipe> Companions(int recipeId) => _companions.GetValueOrDefault(recipeId) ?? [];

        /// <summary>
        /// A side dish is a recipe that is nearly always planned together with another one, and that is the
        /// "lesser" of the pair: it is more often accompanied, goes with more different recipes (rice goes with
        /// many mains), or is quicker (tzatziki with gyros).
        /// </summary>
        private void FindSideDishes()
        {
            var total = new Dictionary<int, double>();
            var accompanied = new Dictionary<int, double>();
            var days = new Dictionary<int, int>();
            var pairs = new Dictionary<(int, int), (double Weight, int Days)>();
            var partners = new Dictionary<int, HashSet<int>>();

            foreach (var (date, ids) in _recipesByDate)
            {
                if (_today.DayNumber - date.DayNumber > HistoryDays) continue;
                var w = Weight(date);
                foreach (var a in ids)
                {
                    total[a] = total.GetValueOrDefault(a) + w;
                    days[a] = days.GetValueOrDefault(a) + 1;
                    if (ids.Count > 1) accompanied[a] = accompanied.GetValueOrDefault(a) + w;
                    foreach (var b in ids.Where(b => b != a))
                    {
                        var (pw, pd) = pairs.GetValueOrDefault((a, b));
                        pairs[(a, b)] = (pw + w, pd + 1);
                        (partners.TryGetValue(a, out var set) ? set : partners[a] = []).Add(b);
                    }
                }
            }

            double Share(int id) => total.TryGetValue(id, out var t) && t > 0 ? accompanied.GetValueOrDefault(id) / t : 0;
            int PartnerCount(int id) => partners.TryGetValue(id, out var set) ? set.Count : 0;
            // Lower = more of a main dish: fewer different partners, more often alone, longer to cook.
            (int, double, int, string) SideRank(int id) =>
                (PartnerCount(id), Share(id), -(ById[id].Duration ?? 0), ById[id].Name ?? "");

            foreach (var (b, _) in days.Where(kv => kv.Value >= 3 && Share(kv.Key) >= SideShare))
            {
                // B is a side of A if A is "more main", and either B goes with many different recipes (rice), or
                // the two are nearly always planned together (gyros + tzatziki). A salad often eaten on its own
                // doesn't turn the steak it sometimes comes with into a side.
                var hasMain = partners[b].Any(a =>
                    pairs[(a, b)].Days >= 2
                    && Comparer<(int, double, int, string)>.Default.Compare(SideRank(a), SideRank(b)) < 0
                    && (PartnerCount(b) >= 3 || Share(a) >= SideShare));
                if (hasMain) _sides.Add(b);
            }

            foreach (var a in total.Keys.Where(a => !_sides.Contains(a)))
            {
                var companions = (partners.GetValueOrDefault(a) ?? [])
                    .Where(b => _sides.Contains(b) && pairs[(a, b)].Days >= 2 && pairs[(a, b)].Weight / total[a] >= CompanionShare)
                    .OrderByDescending(b => pairs[(a, b)].Weight)
                    .Take(MaxCompanions)
                    .Select(b => ById[b])
                    .ToList();
                if (companions.Count > 0) _companions[a] = companions;
            }
        }

        /// <summary>Index of the first date ≥ <paramref name="date"/> in a sorted array.</summary>
        private static int LowerBound(DateOnly[] dates, DateOnly date)
        {
            var index = Array.BinarySearch(dates, date);
            return index >= 0 ? index : ~index;
        }

        /// <summary>Days since the recipe was last cooked before <paramref name="date"/>, or null if never.</summary>
        public int? DaysSinceCooked(Recipe recipe, DateOnly date)
        {
            DateOnly? last = null;
            if (_datesByRecipe.TryGetValue(recipe.Id, out var dates) && LowerBound(dates, date) is > 0 and var i)
                last = dates[i - 1];
            if (recipe.LastCooked is { } lc && DateOnly.FromDateTime(lc) is var lcDate && lcDate < date && (last == null || lcDate > last))
                last = lcDate;
            return last == null ? null : date.DayNumber - last.Value.DayNumber;
        }

        private int TimesCooked(Recipe recipe, DateOnly date, int withinDays) =>
            _datesByRecipe.TryGetValue(recipe.Id, out var dates)
                ? LowerBound(dates, date) - LowerBound(dates, date.AddDays(-withinDays))
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
            var season = Season(date);
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
                x[$"season={season}|cat={category}"] = 1;
                var sameAsYesterday = _recipesByDate.TryGetValue(date.AddDays(-1), out var ids)
                    && ids.Any(id => ById.TryGetValue(id, out var r) && Normalize(r.Category ?? "") == category);
                x["_sameCategoryAsYesterday"] = sameAsYesterday ? 1 : 0;
            }
            foreach (var tag in tags)
            {
                x[$"tag={tag}"] = 1;
                x[$"dow={dow}|tag={tag}"] = 1;
                x[$"season={season}|tag={tag}"] = 1;
            }
            return x;
        }
    }
}
