using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Services;
using static MyCookbook.Services.SharedItemService;

namespace MyCookbook.Api;

/// <summary>
/// History of recipes and cookbooks shared with the current user (MC-71). Guests have no history.
/// </summary>
[ApiController]
[Route("api/shared")]
[Authorize(Policy = "NotGuest")]
public class SharedController(SharedItemService shared) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpGet]
    public async Task<ActionResult<List<SharedItemDto>>> GetAll() =>
        await shared.GetAllAsync(CurrentUser);

    /// <summary>Called after opening a single-recipe share link. Your own recipes are ignored.</summary>
    [HttpPost("recipes/{guid:guid}")]
    public async Task<IActionResult> RecordRecipe(Guid guid) =>
        ToResult(await shared.RecordRecipeAsync(CurrentUser, guid));

    /// <summary>Called after opening a cookbook share link. The token must still be valid.</summary>
    [HttpPost("cookbooks")]
    public async Task<IActionResult> RecordCookbook([FromBody] RecordSharedCookbookRequest request) =>
        ToResult(await shared.RecordCookbookAsync(CurrentUser, request.User, request.ShareToken));

    [HttpPut("{id:int}/pin")]
    public async Task<IActionResult> SetPinned(int id, [FromBody] SetPinnedRequest request) =>
        await shared.SetPinnedAsync(CurrentUser, id, request.Pinned) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id) =>
        await shared.RemoveAsync(CurrentUser, id) ? NoContent() : NotFound();

    private IActionResult ToResult(RecordResult result) => result switch
    {
        RecordResult.NotFound => NotFound(),
        RecordResult.Forbidden => Forbid(),
        _ => NoContent(),
    };
}
