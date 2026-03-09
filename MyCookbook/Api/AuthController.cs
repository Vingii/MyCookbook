using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Data;
using MyCookbook.Services;

namespace MyCookbook.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ApiTokenService tokenService, IConfiguration config, CookbookDatabaseService db) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        username = User.Identity?.Name,
        isAuthenticated = User.Identity?.IsAuthenticated == true,
        isGuest = User.Identity?.Name?.StartsWith("guest-", StringComparison.OrdinalIgnoreCase) == true
    });

    [HttpGet("logout")]
    public IActionResult Logout()
    {
        var authentikUrl = config["COOKBOOK_AUTHENTIK_URL"];
        var cookbookUrl = config["COOKBOOK_URL"];
        var redirectUri = Uri.EscapeDataString(cookbookUrl ?? "/");
        return Redirect($"{authentikUrl}/application/o/cookbook/end-session/?post_logout_redirect_uri={redirectUri}");
    }

    [HttpGet("token")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> GetToken()
    {
        var user = User.Identity!.Name!;
        var existingHash = await tokenService.GetExistingTokenHashAsync(user);
        if (!string.IsNullOrEmpty(existingHash))
            return Ok(new { message = "Token already exists. Delete it first to generate a new one." });

        var token = await tokenService.GenerateTokenAsync(user);
        return Ok(new { token });
    }

    [HttpDelete("token")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> RevokeToken()
    {
        await tokenService.RevokeTokenAsync(User.Identity!.Name!);
        return NoContent();
    }

    [HttpGet("share-token")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> GetShareToken()
    {
        var token = await db.GetUserPreference("ShareToken", User.Identity!.Name!);
        return Ok(new { token = string.IsNullOrEmpty(token) ? null : token });
    }

    [HttpPost("share-token")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> CreateShareToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        await db.UpdateUserPreference("ShareToken", token, User.Identity!.Name!);
        return Ok(new { token });
    }

    [HttpDelete("share-token")]
    [Authorize(Policy = "NotGuest")]
    public async Task<IActionResult> RevokeShareToken()
    {
        await db.UpdateUserPreference("ShareToken", "", User.Identity!.Name!);
        return NoContent();
    }
}
