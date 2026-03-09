using Microsoft.AspNetCore.Mvc;
using MyCookbook.Services;
using System.Reflection;

namespace MyCookbook.Api
{
    [ApiController]
    [Route("api/changelog")]
    public class ChangelogController : ControllerBase
    {
        private readonly ChangelogService _changelogService;

        public ChangelogController(ChangelogService changelogService)
        {
            _changelogService = changelogService;
        }

        [HttpGet]
        public async Task<IActionResult> GetEntries()
        {
            var entries = await _changelogService.GetLatestChangelogEntries(20);
            return Ok(entries.Select(e => new
            {
                e.Version,
                releaseDate = e.ReleaseDate == DateTime.MinValue ? null : (DateTime?)e.ReleaseDate,
                e.RawHtml,
            }));
        }

        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            var version = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion.Split('+').First()
                ?? "0.0.0";
            return Ok(version);
        }
    }
}
