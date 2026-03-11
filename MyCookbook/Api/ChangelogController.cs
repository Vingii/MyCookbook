using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyCookbook.Data;
using MyCookbook.Services;
using System.Reflection;

namespace MyCookbook.Api
{
    [ApiController]
    [Route("api/changelog")]
    public class ChangelogController(ChangelogService changelogService, CookbookDatabaseService db) : ControllerBase
    {
        private string CurrentUser => HttpContext.User.Identity!.Name!;

        private static string CurrentVersion => Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+').First()
            ?? "0.0.0";

        [HttpGet]
        public async Task<IActionResult> GetEntries()
        {
            var entries = await changelogService.GetLatestChangelogEntries(20);
            return Ok(entries.Select(e => new
            {
                e.Version,
                releaseDate = e.ReleaseDate == DateTime.MinValue ? null : (DateTime?)e.ReleaseDate,
                e.RawHtml,
            }));
        }

        [HttpGet("version")]
        public IActionResult GetVersion() => Ok(CurrentVersion);

        [HttpGet("lastSeen")]
        [Authorize(Policy = "CookieOrApiKey")]
        public async Task<IActionResult> GetLastSeen()
        {
            var value = await db.GetUserPreference("LastSeenVersion", CurrentUser);
            return Ok(value);
        }

        [HttpPut("lastSeen")]
        [Authorize(Policy = "CookieOrApiKey")]
        public async Task<IActionResult> MarkAsSeen()
        {
            await db.UpdateUserPreference("LastSeenVersion", CurrentVersion, CurrentUser);
            return Ok();
        }
    }
}
