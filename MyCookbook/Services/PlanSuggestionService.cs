using Microsoft.Extensions.Caching.Memory;
using MyCookbook.Api.Dto;
using MyCookbook.Data;

namespace MyCookbook.Services;

/// <summary>
/// Loads what <see cref="PlanRecommender"/> needs and caches each user's trained model, so it is only retrained
/// when their recipes or plans change (or the day changes), not on every request.
/// </summary>
public class PlanSuggestionService(CookbookDatabaseService db, CookbookService cookbooks, IMemoryCache cache)
{
    /// <summary>Days of suggestions the planner asks for: today and the next 13 days.</summary>
    public const int DefaultDays = 14;

    public async Task<List<PlanSuggestionDto>> SuggestAsync(string user, DateOnly from, DateOnly to, DateOnly today, int count)
    {
        // Enough history for the model, plus plans up to the end of the period for the exclusion rule.
        var since = today.AddDays(-PlanRecommender.HistoryDays - PlanRecommender.ExclusionDays);
        // Recipes in cookbooks hidden from the planner are never suggested (MC-80).
        var shown = await cookbooks.ShownInPlannerAsync(user);
        var recipes = (await db.GetRecipesForSuggestionsAsync(user)).Where(shown).ToList();
        var plans = (await db.GetPlansForSuggestionsAsync(user, since))
            .Select(p => new PlanRecommender.Plan(p.RecipeId, p.Date, p.FromFridge))
            .ToList();

        var key = ("plan-suggestions", user, today, Fingerprint(recipes, plans, user));
        var model = await cache.GetOrCreateAsync(key, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return Task.FromResult(PlanRecommender.Train(recipes, plans, user, today));
        });
        return PlanRecommender.Suggest(model!, plans, from, to, count);
    }

    /// <summary>Changes whenever anything the model learns from changes.</summary>
    private static int Fingerprint(
        IEnumerable<Data.CookbookDatabase.Recipe> recipes, IEnumerable<PlanRecommender.Plan> plans, string user)
    {
        var hash = new HashCode();
        foreach (var r in recipes.OrderBy(r => r.Id))
        {
            hash.Add(r.Id);
            hash.Add(r.Name);
            hash.Add(r.Category);
            hash.Add(r.Duration);
            hash.Add(r.LastCooked);
            hash.Add(r.IsFavorite(user));
            foreach (var t in (r.Tags ?? []).Select(t => t.Name).Order()) hash.Add(t);
        }
        foreach (var p in plans.OrderBy(p => p.Date).ThenBy(p => p.RecipeId)) hash.Add(p);
        return hash.ToHashCode();
    }
}
