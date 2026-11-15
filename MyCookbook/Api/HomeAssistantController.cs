using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Services;

namespace MyCookbook.Api;

/// <summary>Per-user Home Assistant webhook for sending the planner shopping list to an HA to-do list.</summary>
[ApiController]
[Route("api/homeassistant")]
[Authorize(Policy = "CookieOrApiKey")]
[Authorize(Policy = "NotGuest")]
public class HomeAssistantController(CookbookDatabaseService db, HomeAssistantService homeAssistant) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    private async Task<string?> GetWebhookUrl()
    {
        var url = await db.GetUserPreference(HomeAssistantService.PreferenceKey, CurrentUser);
        return string.IsNullOrEmpty(url) ? null : url;
    }

    [HttpGet]
    public async Task<HomeAssistantSettingsDto> Get() => new() { WebhookUrl = await GetWebhookUrl() };

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] HomeAssistantSettingsDto req)
    {
        if (!HomeAssistantService.IsValidWebhookUrl(req.WebhookUrl))
            return BadRequest(new { code = "invalidWebhookUrl" });
        await db.UpdateUserPreference(HomeAssistantService.PreferenceKey, req.WebhookUrl!.Trim(), CurrentUser);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        await db.UpdateUserPreference(HomeAssistantService.PreferenceKey, "", CurrentUser);
        return NoContent();
    }

    [HttpPost("shopping-list")]
    public async Task<IActionResult> SendShoppingList([FromBody] SendShoppingListRequest req)
    {
        var url = await GetWebhookUrl();
        if (url == null) return Conflict(new { code = "notConfigured" });

        var items = req.Items.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
        if (items.Count == 0 || items.Count > HomeAssistantService.MaxItems) return BadRequest();

        return await homeAssistant.SendShoppingListAsync(url, items)
            ? NoContent()
            : StatusCode(StatusCodes.Status502BadGateway, new { code = "homeAssistantFailed" });
    }
}
