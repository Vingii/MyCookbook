using Markdig;
using MyCookbook.Model;
using System.Text.RegularExpressions;

namespace MyCookbook.Services
{
    public class ChangelogService
    {
        public const string DefaultLanguage = "en";

        // Translations live next to CHANGELOG.md as CHANGELOG.<lang>.md
        private static readonly Dictionary<string, string> ChangelogFiles = new()
        {
            [DefaultLanguage] = "CHANGELOG.md",
            ["cs"] = "CHANGELOG.cs.md",
        };

        private readonly string _contentRootPath;
        private Dictionary<string, List<ChangelogEntry>>? _entriesByLanguage;
        private readonly ILogger<ChangelogService> _logger;

        public ChangelogService(IHostEnvironment env, ILogger<ChangelogService> logger)
        {
            _contentRootPath = env.ContentRootPath;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            var entriesByLanguage = new Dictionary<string, List<ChangelogEntry>>();
            foreach (var (language, fileName) in ChangelogFiles)
            {
                entriesByLanguage[language] = await LoadChangelogAsync(Path.Combine(_contentRootPath, fileName));
            }
            _entriesByLanguage = entriesByLanguage;
        }

        private async Task<List<ChangelogEntry>> LoadChangelogAsync(string filePath)
        {
            var entries = new List<ChangelogEntry>();

            if (!File.Exists(filePath))
            {
                _logger.LogError("Changelog not found at {Path}", filePath);
                return entries;
            }

            var markdownContent = await File.ReadAllTextAsync(filePath);

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

        /// <summary>
        /// Returns all entries in the given language. The English changelog is authoritative:
        /// any version missing from the translation falls back to its English entry.
        /// </summary>
        private async Task<List<ChangelogEntry>> GetEntries(string? language)
        {
            if (_entriesByLanguage == null) await InitializeAsync();
            var entriesByLanguage = _entriesByLanguage!;

            var defaultEntries = entriesByLanguage[DefaultLanguage];
            if (language == null || language == DefaultLanguage || !entriesByLanguage.TryGetValue(language, out var translated))
            {
                return defaultEntries;
            }

            var translatedByVersion = translated.ToDictionary(e => e.Version);
            return defaultEntries
                .Select(e => translatedByVersion.GetValueOrDefault(e.Version, e))
                .ToList();
        }

        public async Task<List<ChangelogEntry>> GetLatestChangelogEntries(int count, string language = DefaultLanguage)
        {
            return (await GetEntries(language)).Take(count).ToList();
        }

        public async Task<List<ChangelogEntry>> GetChangelogEntriesSinceVersion(string userLastSeenVersion, string language = DefaultLanguage)
        {
            var entries = await GetEntries(language);

            // Find the index of the user's last seen version
            var lastSeenEntry = entries.FirstOrDefault(e => e.Version == userLastSeenVersion);
            if (lastSeenEntry == null)
            {
                // If userLastSeenVersion is not found, return all entries (or just the latest, depending on desired behavior)
                return entries;
            }

            // Get all entries that are newer than (come before in the sorted list) the user's last seen version
            var index = entries.IndexOf(lastSeenEntry);
            return entries.Take(index).ToList();
        }

        public async Task<ChangelogEntry?> GetChangelogEntryForVersion(string version, string language = DefaultLanguage)
        {
            return (await GetEntries(language)).FirstOrDefault(e => e.Version == version);
        }
    }
}
