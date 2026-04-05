using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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
        username = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? User.Identity?.Name,
        isAuthenticated = User.Identity?.IsAuthenticated == true,
        isGuest = User.Identity?.Name?.StartsWith("guest-", StringComparison.OrdinalIgnoreCase) == true
    });

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) =>
        Challenge(
            new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
            OpenIdConnectDefaults.AuthenticationScheme);

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        if (config["DEV_AUTO_LOGIN"] is { Length: > 0 })
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        return SignOut(
            new AuthenticationProperties { RedirectUri = config["COOKBOOK_URL"] ?? "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
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
