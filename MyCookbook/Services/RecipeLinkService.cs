using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Model;
using MyCookbook.Utils;

namespace MyCookbook.Services;

/// <summary>
/// Resolves the wiki-style <c>[[Recipe name]]</c> links embedded in step descriptions.
/// Links are stored by name rather than by guid so they survive export/import and cloning.
/// </summary>
public class RecipeLinkService(CookbookDatabaseService db)
{
    /// <summary>Every distinct link in the recipe's steps that resolves to one of the user's recipes.</summary>
    public async Task<List<RecipeLinkDto>> GetLinksAsync(Recipe recipe, string user)
    {
        var targets = (recipe.Steps ?? [])
            .SelectMany(s => RecipeLinks.ExtractReferences(s.Description))
            .Select(text => new { Text = text, Key = RecipeLinks.NormalizeKey(text) })
            .DistinctBy(t => t.Key)
            .ToList();

        if (targets.Count == 0) return [];

        var byName = await GetRecipesByKeyAsync(user);

        return targets
            .Where(t => byName.ContainsKey(t.Key))
            .Select(t => new RecipeLinkDto { Text = t.Text, Name = byName[t.Key].Name, Guid = byName[t.Key].Guid })
            .ToList();
    }

    /// <summary>The user's other recipes whose steps link to this one.</summary>
    public async Task<List<RecipeRefDto>> GetBacklinksAsync(Recipe recipe, string user)
    {
        var key = RecipeLinks.NormalizeKey(recipe.Name);
        var steps = await db.GetLinkingStepsAsync(user);

        return steps
            .Where(s => s.RecipeGuid != recipe.Guid)
            .Where(s => RecipeLinks.ExtractReferences(s.Description).Any(t => RecipeLinks.NormalizeKey(t) == key))
            .DistinctBy(s => s.RecipeGuid)
            .OrderBy(s => s.RecipeName, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new RecipeRefDto { Guid = s.RecipeGuid, Name = s.RecipeName })
            .ToList();
    }

    private async Task<Dictionary<string, RecipeRef>> GetRecipesByKeyAsync(string user)
    {
        var map = new Dictionary<string, RecipeRef>();
        foreach (var reference in await db.GetRecipeRefsAsync(user))
        {
            // If two recipe names normalize to the same key, the first one wins.
            map.TryAdd(RecipeLinks.NormalizeKey(reference.Name), reference);
        }
        return map;
    }
}
