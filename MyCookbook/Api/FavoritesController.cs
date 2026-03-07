using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Data;

namespace MyCookbook.Api;

[ApiController]
[Route("api/recipes/{guid:guid}/favorite")]
[Authorize(Policy = "NotGuest")]
public class FavoritesController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpPost]
    public async Task<IActionResult> Add(Guid guid)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();
        await db.AddFavoriteAsync(recipe, CurrentUser);
        return Created("", null);
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(Guid guid)
    {
        var recipe = await db.GetDetailedRecipeAsync(guid, CurrentUser);
        if (recipe == null) return NotFound();
        await db.DeleteFavoriteAsync(recipe.Id, CurrentUser);
        return NoContent();
    }
}
