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
public class PlannerController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;


    [HttpGet]
    public async Task<ActionResult<List<PlannedRecipeDto>>> GetAll(
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var all = await db.GetPlannedRecipesAsync(CurrentUser);
        var flat = all.SelectMany(kv => kv.Value);

        if (DateOnly.TryParse(from, out var fromDate))
            flat = flat.Where(p => p.Date >= fromDate);
        if (DateOnly.TryParse(to, out var toDate))
            flat = flat.Where(p => p.Date <= toDate);

        return flat.Select(p => p.ToDto()).ToList();
    }

    /// <summary>Combined ingredients of the recipes planned to be cooked from <paramref name="from"/> to <paramref name="to"/>.</summary>
    [HttpGet("ingredients")]
    public async Task<ActionResult<List<IngredientSummaryDto>>> GetIngredients(
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        if (!DateOnly.TryParse(from, out var fromDate) || !DateOnly.TryParse(to, out var toDate))
            return BadRequest("Invalid date format");
        if (toDate < fromDate) return BadRequest("'to' is before 'from'");

        var recipes = await db.GetPlannedRecipesToCookAsync(CurrentUser, fromDate, toDate);
        return IngredientSummaryBuilder.Build(recipes);
    }

    /// <summary>
    /// Recipes to plan between <paramref name="from"/> and <paramref name="to"/> (default: today and the next
    /// 13 days), best first. See <see cref="PlanRecommender"/>.
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<List<PlanSuggestionDto>>> GetSuggestions(
        [FromServices] PlanSuggestionService suggestions,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] int count = 10)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var fromDate = today;
        if (from != null && !DateOnly.TryParse(from, out fromDate)) return BadRequest("Invalid date format");
        var toDate = fromDate.AddDays(PlanSuggestionService.DefaultDays - 1);
        if (to != null && !DateOnly.TryParse(to, out toDate)) return BadRequest("Invalid date format");
        if (toDate < fromDate || toDate.DayNumber - fromDate.DayNumber > 62) return BadRequest("Invalid date range");

        return await suggestions.SuggestAsync(CurrentUser, fromDate, toDate, today, Math.Clamp(count, 1, 20));
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
