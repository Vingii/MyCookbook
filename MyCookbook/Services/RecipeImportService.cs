using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Model;

namespace MyCookbook.Services;

/// <summary>
/// Turns a recipe web page into recipe drafts with an LLM (MC-69): one version as written on the page and one
/// translated into the app language and adapted to the user's own conventions (categories, tags, ingredient
/// names, amount format, step style). The user picks one in a preview; nothing is saved until then.
/// </summary>
public class RecipeImportService(CookbookDatabaseService db, RecipePageFetcher fetcher, LlmClient llm, ILogger<RecipeImportService> logger)
{
    // Column limits from CookbookDatabaseContext.
    private const int MaxNameLength = 50;
    private const int MaxIngredients = 60;
    private const int MaxSteps = 40;
    private const int MaxTags = 8;

    public class ImportException(string code, string message) : Exception(message)
    {
        /// <summary>Stable code the UI translates: not_configured, no_recipe, ai_failed, plus the fetcher's codes.</summary>
        public string Code { get; } = code;
    }

    public bool IsEnabled => llm.IsConfigured;

    public async Task<RecipeImportPreviewDto> PreviewAsync(string url, string language, string user, CancellationToken ct = default)
    {
        if (!llm.IsConfigured) throw new ImportException("not_configured", "AI import is not configured");

        RecipePageFetcher.PageContent page;
        try
        {
            page = await fetcher.FetchAsync(url, ct);
        }
        catch (RecipePageFetcher.FetchException ex)
        {
            throw new ImportException(ex.Code, ex.Message);
        }

        var conventions = await GetConventionsAsync(user);
        var targetLanguage = language == "en" ? "English" : "Czech";

        string answer;
        try
        {
            answer = await llm.CompleteAsync(BuildSystemPrompt(targetLanguage), BuildUserPrompt(page, conventions), ct);
        }
        catch (LlmClient.LlmException ex)
        {
            logger.LogError(ex, "AI recipe import failed for {Url}", page.Url);
            throw new ImportException("ai_failed", ex.Message);
        }

        var variants = ParseAnswer(answer);
        return new RecipeImportPreviewDto
        {
            SourceUrl = page.Url,
            Variants = variants.Select(v => new RecipeImportVariantDto { Key = v.Key, Recipe = Sanitize(v.Recipe) }).ToList(),
        };
    }

    /// <summary>Saves the version the user picked as a new recipe, with a unique name.</summary>
    public async Task<Recipe> SaveAsync(RecipeDraftDto draft, string user)
    {
        draft = Sanitize(draft);
        await using var context = await db.GetContext();
        var existingNames = await context.Recipes.Where(r => r.UserName == user).Select(r => r.Name).ToListAsync();

        var recipe = new Recipe
        {
            UserName = user,
            Name = UniqueName(draft.Name, existingNames),
            Category = draft.Category,
            Servings = draft.Servings,
            Duration = draft.Duration,
            Tags = draft.Tags.Select(t => new Tag { UserName = user, Name = t }).ToList(),
        };
        for (var i = 0; i < draft.Ingredients.Count; i++)
        {
            recipe.Ingredients.Add(new Ingredient
            {
                UserName = user, Name = draft.Ingredients[i].Name, Amount = draft.Ingredients[i].Amount, Order = i + 1,
            });
        }
        for (var i = 0; i < draft.Steps.Count; i++)
        {
            var step = draft.Steps[i];
            recipe.Steps.Add(new Step
            {
                UserName = user,
                Description = step.Description,
                Order = i + 1,
                Duration = step.DurationMinutes is > 0 ? TimeSpan.FromMinutes(step.DurationMinutes.Value) : null,
                StepType = Enum.Parse<StepType>(step.StepType),
            });
        }
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();
        return recipe;
    }

    /// <summary>"Gyros" → "Gyros (2)" → "Gyros (3)"… while staying within the name column.</summary>
    public static string UniqueName(string name, ICollection<string> existing)
    {
        if (!existing.Contains(name)) return name;
        for (var i = 2; ; i++)
        {
            var suffix = $" ({i})";
            var candidate = (name.Length + suffix.Length > MaxNameLength ? name[..(MaxNameLength - suffix.Length)].TrimEnd() : name) + suffix;
            if (!existing.Contains(candidate)) return candidate;
        }
    }

    private record Conventions(
        List<string> Categories, List<string> Tags, List<string> Ingredients, List<string> Amounts, List<string> StepExamples);

    private async Task<Conventions> GetConventionsAsync(string user)
    {
        await using var context = await db.GetContext();
        var categories = await context.Recipes.Where(r => r.UserName == user && r.Category != null && r.Category != "")
            .Select(r => r.Category).Distinct().OrderBy(c => c).Take(50).ToListAsync();
        var tags = await context.Tags.Where(t => t.UserName == user)
            .GroupBy(t => t.Name).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(80).ToListAsync();
        var ingredients = await context.Ingredients.Where(i => i.UserName == user && i.Name != null)
            .GroupBy(i => i.Name).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(200).ToListAsync();
        var amounts = await context.Ingredients.Where(i => i.UserName == user && i.Amount != null && i.Amount != "")
            .GroupBy(i => i.Amount).OrderByDescending(g => g.Count()).Select(g => g.Key!).Take(30).ToListAsync();
        var steps = await context.Steps.Where(s => s.UserName == user && s.Description != null && s.Description.Length > 20)
            .OrderByDescending(s => s.Id).Select(s => s.Description).Take(8).ToListAsync();
        return new Conventions(categories, tags, ingredients, amounts, steps);
    }

    private static string BuildSystemPrompt(string targetLanguage) => $$"""
        You extract cooking recipes from web pages for a personal cookbook app.
        Reply with a single JSON object and nothing else: no prose, no Markdown code fences.

        If the page does not contain a recipe, reply exactly: {"error": "no_recipe"}

        Otherwise reply:
        {"suggested": RECIPE, "original": RECIPE}

        RECIPE has this shape:
        {
          "name": string,                 // at most 50 characters
          "category": string | null,      // at most 50 characters
          "servings": integer,
          "duration": integer | null,     // total time in minutes
          "tags": [string],               // at most 8, each at most 50 characters
          "ingredients": [{"name": string, "amount": string | null}],  // each at most 50 characters
          "steps": [{"description": string, "durationMinutes": integer | null, "stepType": "Active" | "SemiPassive" | "Passive"}]
        }

        stepType: "Active" = hands-on work (chopping, frying, kneading); "SemiPassive" = needs occasional attention
        (simmering, boiling); "Passive" = unattended (baking, marinating, rising, chilling).
        Ingredient names never contain the amount. Keep every ingredient and every step of the recipe.

        "original": the recipe as written on the page, in the page's language, with the page's units and wording.
        Only restructure it into the shape above.

        "suggested": the same recipe adapted to this user's cookbook:
        - Written in {{targetLanguage}}, with metric units.
        - When an ingredient is the same as one in the user's ingredient names, use exactly the user's name.
        - Format amounts like the user's amounts (for example "500 g", "2 ks", "1 lžíce").
        - Use one of the user's categories when one fits; otherwise a short new one in the same style.
        - Prefer the user's existing tags; add a new tag only when none fits.
        - Write steps in the style of the user's example steps: concise, imperative, one action per step.
        """;

    private static string BuildUserPrompt(RecipePageFetcher.PageContent page, Conventions c)
    {
        static string List(IEnumerable<string> items) => items.Any() ? string.Join(" | ", items) : "(none yet)";

        var sb = new StringBuilder();
        sb.AppendLine("THE USER'S CONVENTIONS");
        sb.AppendLine($"Categories: {List(c.Categories)}");
        sb.AppendLine($"Tags: {List(c.Tags)}");
        sb.AppendLine($"Ingredient names: {List(c.Ingredients)}");
        sb.AppendLine($"Amounts: {List(c.Amounts)}");
        sb.AppendLine("Example steps:");
        foreach (var step in c.StepExamples) sb.AppendLine($"- {step}");
        if (c.StepExamples.Count == 0) sb.AppendLine("(none yet)");
        sb.AppendLine();
        sb.AppendLine("THE PAGE");
        sb.AppendLine($"URL: {page.Url}");
        sb.AppendLine($"Title: {page.Title}");
        if (page.RecipeJsonLd != null)
        {
            sb.AppendLine("Structured recipe data from the page (schema.org JSON-LD):");
            sb.AppendLine(page.RecipeJsonLd.Length > 20_000 ? page.RecipeJsonLd[..20_000] : page.RecipeJsonLd);
            sb.AppendLine();
            sb.AppendLine("Page text (for anything missing above):");
            sb.AppendLine(page.Text.Length > 15_000 ? page.Text[..15_000] : page.Text);
        }
        else
        {
            sb.AppendLine("Page text:");
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static List<(string Key, RecipeDraftDto Recipe)> ParseAnswer(string answer)
    {
        // Models sometimes wrap JSON in code fences or add a sentence around it.
        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start) throw new ImportException("ai_failed", "The AI reply contained no JSON");

        try
        {
            using var doc = JsonDocument.Parse(answer[start..(end + 1)]);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _)) throw new ImportException("no_recipe", "No recipe found on the page");

            var variants = new List<(string, RecipeDraftDto)>();
            foreach (var key in new[] { "suggested", "original" })
            {
                if (root.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Object)
                {
                    var recipe = ReadRecipe(element);
                    if (recipe.Ingredients.Count > 0 || recipe.Steps.Count > 0) variants.Add((key, recipe));
                }
            }
            if (variants.Count == 0) throw new ImportException("no_recipe", "The AI found no ingredients or steps");
            return variants;
        }
        catch (JsonException ex)
        {
            throw new ImportException("ai_failed", $"The AI reply was not valid JSON: {ex.Message}");
        }
    }

    /// <summary>Lenient reading: models mix up numbers and strings, so read field by field.</summary>
    private static RecipeDraftDto ReadRecipe(JsonElement e) => new()
    {
        Name = Str(e, "name") ?? "",
        Category = Str(e, "category"),
        Servings = Int(e, "servings") ?? 1,
        Duration = Int(e, "duration"),
        Tags = Arr(e, "tags").Select(t => t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "").ToList(),
        Ingredients = Arr(e, "ingredients").Where(i => i.ValueKind == JsonValueKind.Object)
            .Select(i => new RecipeDraftIngredientDto { Name = Str(i, "name") ?? "", Amount = Str(i, "amount") }).ToList(),
        Steps = Arr(e, "steps").Where(s => s.ValueKind == JsonValueKind.Object)
            .Select(s => new RecipeDraftStepDto
            {
                Description = Str(s, "description") ?? "",
                DurationMinutes = Int(s, "durationMinutes"),
                StepType = Str(s, "stepType") ?? "Active",
            }).ToList(),
    };

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;

    private static int? Int(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return (int)Math.Round(d);
        if (v.ValueKind == JsonValueKind.String && Regex.Match(v.GetString() ?? "", @"\d+") is { Success: true } m) return int.Parse(m.Value);
        return null;
    }

    private static IEnumerable<JsonElement> Arr(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToList() : [];

    /// <summary>
    /// Enforces the database limits on anything coming from the AI or the client: trims, truncates to column
    /// sizes, drops empty rows, clamps numbers and normalizes step types.
    /// </summary>
    public static RecipeDraftDto Sanitize(RecipeDraftDto d)
    {
        static string Clean(string? s, int max) => Truncate(Regex.Replace(s ?? "", @"\s+", " ").Trim(), max);
        static string Truncate(string s, int max) => s.Length > max ? s[..max].TrimEnd() : s;

        var name = Clean(d.Name, MaxNameLength);
        var category = Clean(d.Category, MaxNameLength);
        return new RecipeDraftDto
        {
            Name = name.Length > 0 ? name : "Recept",
            Category = category.Length > 0 ? category : null,
            Servings = Math.Clamp(d.Servings, 1, 100),
            Duration = d.Duration is > 0 ? Math.Min(d.Duration.Value, 7 * 24 * 60) : null,
            Tags = d.Tags.Select(t => Clean(t, MaxNameLength)).Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTags).ToList(),
            Ingredients = d.Ingredients
                .Select(i => new RecipeDraftIngredientDto
                {
                    Name = Clean(i.Name, MaxNameLength),
                    Amount = Clean(i.Amount, MaxNameLength) is { Length: > 0 } a ? a : null,
                })
                .Where(i => i.Name.Length > 0).Take(MaxIngredients).ToList(),
            Steps = d.Steps
                .Select(s => new RecipeDraftStepDto
                {
                    Description = Clean(s.Description, 4000),
                    DurationMinutes = s.DurationMinutes is > 0 ? Math.Min(s.DurationMinutes.Value, 7 * 24 * 60) : null,
                    // TryParse also accepts numbers like "7", so require a defined value.
                    StepType = Enum.TryParse<StepType>(s.StepType, ignoreCase: true, out var type) && Enum.IsDefined(type)
                        ? type.ToString() : nameof(StepType.Active),
                })
                .Where(s => s.Description.Length > 0).Take(MaxSteps).ToList(),
        };
    }
}
