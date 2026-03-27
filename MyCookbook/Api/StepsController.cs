using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Api;

[ApiController]
[Route("api/recipes/{guid:guid}/steps")]
[Authorize(Policy = "NotGuest")]
public class StepsController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpPost]
    public async Task<ActionResult<StepDto>> Create(Guid guid, [FromBody] CreateStepRequest req)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var maxOrder = recipe.Steps?.Any() == true ? recipe.Steps.Max(s => s.Order) : 0;
        var step = new Step
        {
            RecipeId = recipe.Id,
            Description = req.Description,
            Order = maxOrder + 1,
            Duration = req.DurationSeconds.HasValue ? TimeSpan.FromSeconds(req.DurationSeconds.Value) : null,
            StepType = DtoMapper.ParseStepType(req.StepType)
        };
        var created = await db.CreateStepAsync(step, CurrentUser);
        return Created("", created.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(Guid guid, int id, [FromBody] UpdateStepRequest req)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var step = recipe.Steps?.FirstOrDefault(s => s.Id == id);
        if (step == null) return NotFound();

        step.Description = req.Description;
        step.Duration = req.DurationSeconds.HasValue ? TimeSpan.FromSeconds(req.DurationSeconds.Value) : null;
        step.StepType = DtoMapper.ParseStepType(req.StepType);
        await db.UpdateStepAsync(step, CurrentUser);
        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(Guid guid, int id)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var step = recipe.Steps?.FirstOrDefault(s => s.Id == id);
        if (step == null) return NotFound();

        await db.DeleteStepAsync(step, CurrentUser);
        return NoContent();
    }

    [HttpPost("{id:int}/up")]
    public async Task<IActionResult> MoveUp(Guid guid, int id)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var step = recipe.Steps?.FirstOrDefault(s => s.Id == id);
        if (step == null) return NotFound();

        await db.IncreaseStepOrder(step, CurrentUser);
        return Ok();
    }

    [HttpPost("{id:int}/down")]
    public async Task<IActionResult> MoveDown(Guid guid, int id)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var step = recipe.Steps?.FirstOrDefault(s => s.Id == id);
        if (step == null) return NotFound();

        await db.DecreaseStepOrder(step, CurrentUser);
        return Ok();
    }
}
