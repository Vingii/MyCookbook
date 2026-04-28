using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Api;

[ApiController]
[Authorize(Policy = "CookieOrApiKey")]
public class TagsController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpGet("api/tags")]
    public async Task<ActionResult<List<string>>> GetAll([FromQuery] string? user)
    {
        var tags = await db.GetAllTags(user ?? CurrentUser);
        return tags.Select(t => t.Name).Distinct().OrderBy(n => n).ToList();
    }

    [HttpGet("api/categories")]
    public async Task<ActionResult<List<string>>> GetAllCategories([FromQuery] string? user)
    {
        return await db.GetAllCategoriesAsync(user ?? CurrentUser);
    }

    [HttpPost("api/recipes/{guid:guid}/tags")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> AddTag(Guid guid, [FromBody] AddTagRequest req)
    {
        var context = await db.GetContext();
        var recipe = await context.Recipes
            .FirstOrDefaultAsync(r => r.Guid == guid && r.UserName == CurrentUser);
        if (recipe == null) return NotFound();

        var exists = await context.Tags
            .AnyAsync(t => t.RecipeId == recipe.Id && t.UserName == CurrentUser && t.Name == req.Name);
        if (!exists)
        {
            context.Tags.Add(new Tag { UserName = CurrentUser, RecipeId = recipe.Id, Name = req.Name });
            await context.SaveChangesAsync();
        }
        return Created("", null);
    }

    [HttpDelete("api/recipes/{guid:guid}/tags/{name}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> DeleteTag(Guid guid, string name)
    {
        var context = await db.GetContext();
        var recipe = await context.Recipes
            .FirstOrDefaultAsync(r => r.Guid == guid && r.UserName == CurrentUser);
        if (recipe == null) return NotFound();

        var tag = await context.Tags
            .FirstOrDefaultAsync(t => t.RecipeId == recipe.Id && t.UserName == CurrentUser && t.Name == name);
        if (tag == null) return NotFound();

        context.Tags.Remove(tag);
        await context.SaveChangesAsync();
        return NoContent();
    }
}
