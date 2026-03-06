using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "CookieOrApiKey")]
public class RecipesController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpGet]
    public async Task<ActionResult<List<RecipeDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? tag)
    {
        var recipes = await db.GetRecipesAsync(CurrentUser);

        if (!string.IsNullOrWhiteSpace(search))
            recipes = recipes.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(category))
            recipes = recipes.Where(r => r.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (!string.IsNullOrWhiteSpace(tag))
            recipes = recipes.Where(r => r.Tags != null && r.Tags.Any(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase))).ToList();

        return recipes.Select(r => r.ToDto(CurrentUser)).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create([FromBody] CreateRecipeRequest req)
    {
        var recipe = new Recipe
        {
            Name = req.Name,
            Category = req.Category,
            Duration = req.Duration,
            Servings = req.Servings
        };
        var created = await db.CreateRecipeAsync(recipe, CurrentUser);
        return CreatedAtAction(nameof(GetById), new { guid = created.Guid }, created.ToDto(CurrentUser));
    }

    [HttpGet("random")]
    public async Task<ActionResult<RecipeDto>> GetRandom()
    {
        var recipes = await db.GetRecipesAsync(CurrentUser);
        if (recipes.Count == 0) return NotFound();
        var recipe = recipes[Random.Shared.Next(recipes.Count)];
        return recipe.ToDto(CurrentUser);
    }

    [HttpGet("{guid:guid}")]
    public async Task<ActionResult<RecipeDto>> GetById(Guid guid)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();
        return recipe.ToDto(CurrentUser);
    }

    [HttpPut("{guid:guid}")]
    public async Task<IActionResult> Update(Guid guid, [FromBody] UpdateRecipeRequest req)
    {
        var existing = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (existing == null) return NotFound();

        existing.Name = req.Name;
        existing.Category = req.Category;
        existing.Duration = req.Duration;
        existing.Servings = req.Servings;

        await db.UpdateRecipeAsync(existing, CurrentUser);
        return Ok();
    }

    [HttpDelete("{guid:guid}")]
    public async Task<IActionResult> Delete(Guid guid)
    {
        var existing = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (existing == null) return NotFound();
        await db.DeleteRecipeAsync(existing, CurrentUser);
        return NoContent();
    }

    [HttpPost("{guid:guid}/clone")]
    public async Task<ActionResult<RecipeDto>> Clone(Guid guid)
    {
        var existing = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (existing == null) return NotFound();
        var cloned = await db.CloneRecipeAsync(existing, CurrentUser);
        return CreatedAtAction(nameof(GetById), new { guid = cloned.Guid }, cloned.ToDto(CurrentUser));
    }

    [HttpPost("{guid:guid}/lastcooked")]
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
        return recipe.ToDto(recipe.UserName);
    }
}
