using System.Globalization;
using System.Text;
using FuzzySharp;
using FuzzySharp.SimilarityRatio;
using FuzzySharp.SimilarityRatio.Scorer.StrategySensitive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Services;

namespace MyCookbook.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "CookieOrApiKey")]
public class RecipesController(CookbookDatabaseService db, RecipeLinkService recipeLinks, CookbookService cookbooks) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    private static string NormalizeName(string s) =>
        new string(
            s.Normalize(NormalizationForm.FormD)
             .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
             .ToArray()
        ).ToLowerInvariant();

    /// <summary>
    /// Narrows <paramref name="recipes"/> to one of your cookbooks and/or to the cookbooks shown in the planner.
    /// Null when the cookbook isn't yours.
    /// </summary>
    private async Task<List<Recipe>?> FilterByCookbook(List<Recipe> recipes, int? cookbook, bool planner)
    {
        if (cookbook != null)
        {
            var inCookbook = await cookbooks.InCookbookAsync(CurrentUser, cookbook.Value);
            if (inCookbook == null) return null;
            recipes = recipes.Where(inCookbook).ToList();
        }
        if (planner)
        {
            var shown = await cookbooks.ShownInPlannerAsync(CurrentUser);
            recipes = recipes.Where(shown).ToList();
        }
        return recipes;
    }

    [HttpGet]
    public async Task<ActionResult<List<RecipeDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? tag,
        [FromQuery] int? cookbook = null,
        [FromQuery] bool planner = false)
    {
        var recipes = await db.GetRecipesAsync(CurrentUser);
        var filtered = await FilterByCookbook(recipes, cookbook, planner);
        if (filtered == null) return NotFound();
        recipes = filtered;

        if (!string.IsNullOrWhiteSpace(category))
            recipes = recipes.Where(r => r.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (!string.IsNullOrWhiteSpace(tag))
            recipes = recipes.Where(r => r.Tags != null && r.Tags.Any(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase))).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var threshold = search.Length <= 3 ? 60 : 70;
            var results = Process.ExtractAll(
                new Recipe { Name = search },
                recipes,
                r => NormalizeName(r.Name),
                scorer: ScorerCache.Get<PartialTokenSortScorer>()
            );
            recipes = results
                .Where(x => x.Score >= threshold)
                .Select(x => x.Value)
                .ToList();
        }

        return recipes.Select(r => r.ToDto(CurrentUser)).ToList();
    }

    [HttpPost]
    [Authorize(Policy = "NotGuest")]
    public async Task<ActionResult<RecipeDto>> Create([FromBody] CreateRecipeRequest req)
    {
        var recipe = new Recipe
        {
            Name = req.Name,
            Category = req.Category,
            Duration = req.Duration,
            Servings = req.Servings
        };
        var (found, cookbookId) = await cookbooks.ResolveAsync(CurrentUser, req.CookbookId);
        if (!found) return BadRequest("Unknown cookbook");
        recipe.CookbookId = cookbookId;
        var created = await db.CreateRecipeAsync(recipe, CurrentUser);
        return CreatedAtAction(nameof(GetById), new { guid = created.Guid }, created.ToDto(CurrentUser));
    }

    /// <summary>Recipe names, for the [[wiki link]] autocomplete in the step editor.</summary>
    [HttpGet("names")]
    public async Task<ActionResult<List<string>>> GetNames()
    {
        var refs = await db.GetRecipeRefsAsync(CurrentUser);
        return refs
            .Select(r => r.Name)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    [HttpGet("random")]
    public async Task<ActionResult<RecipeDto>> GetRandom([FromQuery] int? cookbook = null)
    {
        var recipes = await FilterByCookbook(await db.GetRecipesAsync(CurrentUser), cookbook, planner: false);
        if (recipes == null) return NotFound();
        if (recipes.Count == 0) return NotFound();
        var recipe = recipes[Random.Shared.Next(recipes.Count)];
        return recipe.ToDto(CurrentUser);
    }

    [HttpGet("{guid:guid}")]
    public async Task<ActionResult<RecipeDto>> GetById(Guid guid)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();
        return recipe.ToDto(
            CurrentUser,
            await recipeLinks.GetLinksAsync(recipe, CurrentUser),
            await recipeLinks.GetBacklinksAsync(recipe, CurrentUser));
    }

    [HttpPut("{guid:guid}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Update(Guid guid, [FromBody] UpdateRecipeRequest req)
    {
        var existing = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (existing == null) return NotFound();

        var previousName = existing.Name;

        existing.Name = req.Name;
        existing.Category = req.Category;
        existing.Duration = req.Duration;
        existing.Servings = req.Servings;
        if (req.CookbookId != null)
        {
            var (found, cookbookId) = await cookbooks.ResolveAsync(CurrentUser, req.CookbookId);
            if (!found) return BadRequest("Unknown cookbook");
            existing.CookbookId = cookbookId;
        }

        await db.UpdateRecipeAsync(existing, CurrentUser);

        // Links point at recipes by name, so a rename has to be carried into every step that
        // references this recipe. Only the reference is rewritten — display text is left alone.
        if (!string.Equals(previousName, req.Name, StringComparison.Ordinal))
            await db.RenameRecipeReferencesAsync(previousName, req.Name, CurrentUser);

        return Ok();
    }

    [HttpDelete("{guid:guid}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Delete(Guid guid)
    {
        var existing = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (existing == null) return NotFound();
        await db.DeleteRecipeAsync(existing, CurrentUser);
        return NoContent();
    }

    [HttpPost("{guid:guid}/clone")]
    [Authorize(Policy = "NotGuest")]
    public async Task<ActionResult<RecipeDto>> Clone(Guid guid, [FromQuery] int? cookbook = null)
    {
        // Any recipe can be cloned into your own cookbook by its guid. This grants nothing new:
        // the guid alone already makes a recipe readable through the public GetShared endpoint.
        var existing = await db.GetDetailedRecipeByIdAsync(guid.ToString());
        if (existing == null) return NotFound();
        // Without a cookbook, a copy of your own recipe stays next to it; anyone else's goes to your default one.
        int? cookbookId = existing.UserName == CurrentUser ? existing.CookbookId : null;
        if (cookbook != null)
        {
            (var found, cookbookId) = await cookbooks.ResolveAsync(CurrentUser, cookbook);
            if (!found) return BadRequest("Unknown cookbook");
        }
        var cloned = await db.CloneRecipeAsync(existing, CurrentUser, cookbookId);
        return CreatedAtAction(nameof(GetById), new { guid = cloned.Guid }, cloned.ToDto(CurrentUser));
    }

    [HttpPost("{guid:guid}/lastcooked")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> MarkCooked(Guid guid)
    {
        var existing = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (existing == null) return NotFound();
        await db.UpdateRecipeLastCookedAsync(existing, CurrentUser);
        return Ok();
    }

    [HttpGet("shared/{guid:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RecipeDto>> GetShared(Guid guid)
    {
        var recipe = await db.GetDetailedRecipeByIdAsync(guid.ToString());
        if (recipe == null) return NotFound();
        // No links or backlinks: they would open recipes the owner did not share.
        return recipe.ToDto(recipe.UserName);
    }
}
