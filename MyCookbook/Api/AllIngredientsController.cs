using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Data;

namespace MyCookbook.Api;

[ApiController]
[Route("api/ingredients")]
[Authorize(Policy = "CookieOrApiKey")]
public class AllIngredientsController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpGet]
    public async Task<ActionResult<List<string>>> GetAll()
    {
        return await db.GetAllIngredientNamesAsync(CurrentUser);
    }
}
