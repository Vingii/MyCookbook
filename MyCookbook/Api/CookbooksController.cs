using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Services;

namespace MyCookbook.Api;

/// <summary>The current user's cookbooks (MC-80): separate recipe collections, managed in Settings.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "CookieOrApiKey")]
public class CookbooksController(CookbookService cookbooks) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpGet]
    public async Task<ActionResult<List<CookbookDto>>> GetAll()
    {
        var all = await cookbooks.GetAllAsync(CurrentUser);
        return all.Select(c => ToDto(c.Cookbook, c.IsDefault, c.RecipeCount)).ToList();
    }

    [HttpPost]
    [Authorize(Policy = "NotGuest")]
    public async Task<ActionResult<CookbookDto>> Create([FromBody] SaveCookbookRequest req)
    {
        var name = req.Name.Trim();
        if (!IsValidName(name)) return BadRequest("Invalid name");
        var created = await cookbooks.CreateAsync(CurrentUser, name, req);
        return Ok(ToDto(created, isDefault: false, recipeCount: 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveCookbookRequest req)
    {
        var name = req.Name.Trim();
        if (!IsValidName(name)) return BadRequest("Invalid name");
        var updated = await cookbooks.UpdateAsync(CurrentUser, id, name, req);
        return updated == null ? NotFound() : Ok();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> Delete(int id) =>
        await cookbooks.DeleteAsync(CurrentUser, id) switch
        {
            CookbookService.DeleteResult.Deleted => NoContent(),
            CookbookService.DeleteResult.NotFound => NotFound(),
            CookbookService.DeleteResult.NotEmpty => Conflict("The cookbook still has recipes"),
            _ => Conflict("The last cookbook can't be deleted"),
        };

    private static CookbookDto ToDto(Data.CookbookDatabase.Cookbook c, bool isDefault, int recipeCount) => new()
    {
        Id = c.Id,
        Name = c.Name,
        ShowInPlanner = c.ShowInPlanner,
        ShowDuration = c.ShowDuration,
        ShowLastCooked = c.ShowLastCooked,
        ShowServings = c.ShowServings,
        ShowStepTimeline = c.ShowStepTimeline,
        IsDefault = isDefault,
        RecipeCount = recipeCount,
    };

    private static bool IsValidName(string name) =>
        name.Length is > 0 and <= CookbookService.MaxNameLength;
}
