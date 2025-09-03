using Markdig;
using MyCookbook.Model;
using System.Text.RegularExpressions;

namespace MyCookbook.Services
{
    public class ChangelogService
    {
        private readonly string _changelogFilePath;
        private List<ChangelogEntry> _allChangelogEntries;
        private readonly ILogger<ChangelogService> _logger;

        public ChangelogService(IHostEnvironment env, ILogger<ChangelogService> logger)
        {
            _changelogFilePath = Path.Combine(env.ContentRootPath, "CHANGELOG.md");
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            _allChangelogEntries = await LoadChangelogAsync();
        }

        private async Task<List<ChangelogEntry>> LoadChangelogAsync()
        {
            var entries = new List<ChangelogEntry>();

            if (!File.Exists(_changelogFilePath))
            {
                _logger.LogError("CHANGELOG.md not found at {Path}", _changelogFilePath);
                return entries;
            }

            var markdownContent = await File.ReadAllTextAsync(_changelogFilePath);

            // Regular expression to find each version block
            var versionRegex = new Regex(
                @"^## \[(?<version>.*?)\](?: - (?<date>\d{4}-\d{2}-\d{2}))?\s*\n(?<content>.*?)(?=\n^## \[|\Z)",
                RegexOptions.Multiline | RegexOptions.Singleline
            );

            foreach (Match versionMatch in versionRegex.Matches(markdownContent))
            {
                var version = versionMatch.Groups["version"].Value.Trim();
                var dateString = versionMatch.Groups["date"].Value;
                var content = versionMatch.Groups["content"].Value.Trim();

                DateTime releaseDate = DateTime.MinValue;
                if (!string.IsNullOrEmpty(dateString) && DateTime.TryParse(dateString, out var parsedDate))
                {
                    releaseDate = parsedDate;
                }

                var htmlContent = Markdown.ToHtml(content);

                entries.Add(new ChangelogEntry
                {
                    Version = version,
                    ReleaseDate = releaseDate,
                    RawHtml = htmlContent
                });
            }

            return entries.OrderByDescending(e => e.ReleaseDate).ToList();
        }

        public async Task<List<ChangelogEntry>> GetLatestChangelogEntries(int count)
        {
            if (_allChangelogEntries == null) await InitializeAsync();
            return _allChangelogEntries?.Take(count).ToList() ?? new List<ChangelogEntry>();
        }

        public async Task<List<ChangelogEntry>> GetChangelogEntriesSinceVersion(string userLastSeenVersion)
        {
            if (_allChangelogEntries == null) await InitializeAsync();

            // Find the index of the user's last seen version
            var lastSeenEntry = _allChangelogEntries?.FirstOrDefault(e => e.Version == userLastSeenVersion);
            if (lastSeenEntry == null)
            {
                // If userLastSeenVersion is not found, return all entries (or just the latest, depending on desired behavior)
                return _allChangelogEntries ?? new List<ChangelogEntry>();
            }

            // Get all entries that are newer than (come before in the sorted list) the user's last seen version
            var index = _allChangelogEntries.IndexOf(lastSeenEntry);
            return _allChangelogEntries.Take(index).ToList();
        }

        public async Task<ChangelogEntry> GetChangelogEntryForVersion(string version)
        {
            if (_allChangelogEntries == null) await InitializeAsync();
            return _allChangelogEntries?.FirstOrDefault(e => e.Version == version);
        }
    }
}
