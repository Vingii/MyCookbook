using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Data;

namespace MyCookbook.Api;

[ApiController]
[Authorize(Policy = "CookieOrApiKey")]
public class ExportController(CookbookDatabaseService db) : ControllerBase
{
    private string CurrentUser => HttpContext.User.Identity!.Name!;

    [HttpGet("api/export")]
    public async Task<IActionResult> Export()
    {
        var json = await db.Export(CurrentUser);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", "cookbook-export.json");
    }

    [HttpPost("api/import")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file provided");

        using var reader = new StreamReader(file.OpenReadStream());
        var json = await reader.ReadToEndAsync();
        await db.Import(json, CurrentUser);
        return Ok();
    }
}
