using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Api;

[ApiController]
[Route("api/recipes/{guid:guid}/ingredients")]
[Authorize(Policy = "CookieOrApiKey")]
public class IngredientsController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpPost]
    public async Task<ActionResult<IngredientDto>> Create(Guid guid, [FromBody] CreateIngredientRequest req)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var maxOrder = recipe.Ingredients?.Any() == true ? recipe.Ingredients.Max(i => i.Order) : 0;
        var ingredient = new Ingredient
        {
            RecipeId = recipe.Id,
            Name = req.Name,
            Amount = req.Amount,
            Order = maxOrder + 1
        };
        var created = await db.CreateIngredientAsync(ingredient, CurrentUser);
        return Created("", created.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(Guid guid, int id, [FromBody] UpdateIngredientRequest req)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var ingredient = recipe.Ingredients?.FirstOrDefault(i => i.Id == id);
        if (ingredient == null) return NotFound();

        ingredient.Name = req.Name;
        ingredient.Amount = req.Amount;
        await db.UpdateIngredientAsync(ingredient, CurrentUser);
        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(Guid guid, int id)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var ingredient = recipe.Ingredients?.FirstOrDefault(i => i.Id == id);
        if (ingredient == null) return NotFound();

        await db.DeleteIngredientAsync(ingredient, CurrentUser);
        return NoContent();
    }

    [HttpPost("{id:int}/up")]
    public async Task<IActionResult> MoveUp(Guid guid, int id)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var ingredient = recipe.Ingredients?.FirstOrDefault(i => i.Id == id);
        if (ingredient == null) return NotFound();

        await db.IncreaseIngredientOrder(ingredient, CurrentUser);
        return Ok();
    }

    [HttpPost("{id:int}/down")]
    public async Task<IActionResult> MoveDown(Guid guid, int id)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();

        var ingredient = recipe.Ingredients?.FirstOrDefault(i => i.Id == id);
        if (ingredient == null) return NotFound();

        await db.DecreaseIngredientOrder(ingredient, CurrentUser);
        return Ok();
    }
}
