using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "CookieOrApiKey")]
public class PlannerController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    private async Task<string> ResolveUser(string? user) =>
        user != null ? await db.ResolveUserIdAsync(user) : CurrentUser;

    private async Task<bool> ValidateShareAccess(string targetUser, string? shareToken)
    {
        if (targetUser == CurrentUser) return true;
        if (string.IsNullOrEmpty(shareToken)) return false;
        var stored = await db.GetUserPreference("ShareToken", targetUser);
        return !string.IsNullOrEmpty(stored) && stored == shareToken;
    }

    [HttpGet]
    public async Task<ActionResult<List<PlannedRecipeDto>>> GetAll(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? user,
        [FromQuery] string? shareToken)
    {
        var targetUser = await ResolveUser(user);
        if (!await ValidateShareAccess(targetUser, shareToken)) return Forbid();
        var all = await db.GetPlannedRecipesAsync(targetUser);
        var flat = all.SelectMany(kv => kv.Value);

        if (DateOnly.TryParse(from, out var fromDate))
            flat = flat.Where(p => p.Date >= fromDate);
        if (DateOnly.TryParse(to, out var toDate))
            flat = flat.Where(p => p.Date <= toDate);

        return flat.Select(p => p.ToDto()).ToList();
    }

    [HttpPost]
    [Authorize(Policy = "NotGuest")]
    public async Task<ActionResult<PlannedRecipeDto>> Create([FromBody] CreatePlannedRecipeRequest req)
    {
        if (!DateOnly.TryParse(req.Date, out var date))
            return BadRequest("Invalid date format");

        var context = await db.GetContext();
        var recipe = context.Recipes.FirstOrDefault(r => r.Guid == req.RecipeGuid && r.UserName == CurrentUser);
        if (recipe == null) return NotFound("Recipe not found");

        var planned = new PlannedRecipe
        {
            RecipeId = recipe.Id,
            Date = date,
            FromFridge = req.FromFridge
        };
        var created = await db.CreatePlannedRecipeAsync(planned, CurrentUser);
        created.Recipe = recipe;
        return Created("", created.ToDto());
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePlannedRecipeRequest req)
    {
        if (!DateOnly.TryParse(req.Date, out var date))
            return BadRequest("Invalid date format");

        var planned = new PlannedRecipe { Id = id, Date = date, FromFridge = req.FromFridge };
        var ok = await db.UpdatePlannedRecipeAsync(planned, CurrentUser);
        return ok ? Ok() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Delete(int id)
    {
        var planned = new PlannedRecipe { Id = id };
        var ok = await db.DeletePlannedRecipeAsync(planned, CurrentUser);
        return ok ? NoContent() : NotFound();
    }
}
