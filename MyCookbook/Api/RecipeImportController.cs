using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Services;

namespace MyCookbook.Api;

/// <summary>Importing a recipe from a web page link with AI (MC-69).</summary>
[ApiController]
[Route("api/recipe-import")]
[Authorize(Policy = "NotGuest")]
public class RecipeImportController(RecipeImportService import) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpGet("status")]
    public RecipeImportStatusDto Status() => new() { Enabled = import.IsEnabled };

    /// <summary>
    /// Downloads the page and returns the versions to choose from. Nothing is saved.
    /// Errors are 422 with a <c>code</c> the UI translates.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<RecipeImportPreviewDto>> Preview([FromBody] RecipeImportPreviewRequest request, CancellationToken ct)
    {
        try
        {
            return await import.PreviewAsync(request.Url, request.Language, CurrentUser, ct);
        }
        catch (RecipeImportService.ImportException ex)
        {
            var status = ex.Code == "not_configured" ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, new { code = ex.Code });
        }
    }

    /// <summary>Saves the chosen version as a new recipe in the current user's cookbook.</summary>
    [HttpPost("save")]
    public async Task<ActionResult<RecipeDto>> Save([FromBody] RecipeDraftDto draft)
    {
        var recipe = await import.SaveAsync(draft, CurrentUser);
        return CreatedAtAction(nameof(RecipesController.GetById), "Recipes", new { guid = recipe.Guid }, recipe.ToDto(CurrentUser));
    }
}
