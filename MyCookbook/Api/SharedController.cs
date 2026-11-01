using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Services;
using static MyCookbook.Services.SharedItemService;

namespace MyCookbook.Api;

/// <summary>
/// History of recipes shared with the current user (MC-71). Guests have no history.
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

    /// <summary>Called after opening a shared recipe link. Your own recipes are ignored.</summary>
    [HttpPost("recipes/{guid:guid}")]
    public async Task<IActionResult> RecordRecipe(Guid guid) =>
        ToResult(await shared.RecordRecipeAsync(CurrentUser, guid));

    [HttpPut("{id:int}/pin")]
    public async Task<IActionResult> SetPinned(int id, [FromBody] SetPinnedRequest request) =>
        await shared.SetPinnedAsync(CurrentUser, id, request.Pinned) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id) =>
        await shared.RemoveAsync(CurrentUser, id) ? NoContent() : NotFound();

    private IActionResult ToResult(RecordResult result) => result switch
    {
        RecordResult.NotFound => NotFound(),
        _ => NoContent(),
    };
}
