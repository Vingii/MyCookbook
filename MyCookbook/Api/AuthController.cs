using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Services;

namespace MyCookbook.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ApiTokenService tokenService) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        username = User.Identity?.Name,
        isAuthenticated = User.Identity?.IsAuthenticated == true
    });

    [HttpGet("token")]
    [Authorize(Policy = "CookieOrApiKey")]
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
    [Authorize(Policy = "CookieOrApiKey")]
    public async Task<IActionResult> RevokeToken()
    {
        await tokenService.RevokeTokenAsync(User.Identity!.Name!);
        return NoContent();
    }
}
