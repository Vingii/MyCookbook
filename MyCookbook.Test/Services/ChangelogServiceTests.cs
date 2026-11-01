using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyCookbook.Services;

namespace MyCookbook.Test.Services
{
    public class ChangelogServiceTests : IDisposable
    {
        private readonly string _contentRoot;

        private const string EnglishChangelog = """
            # Changelog

            ## [1.1.0] - 2025-09-03
            ### Added
            - Step timeline.

            ## [1.0.0] - 2025-09-01
            ### Added
            - Initial public release.
            """;

        public ChangelogServiceTests()
        {
            _contentRoot = Path.Combine(Path.GetTempPath(), "changelog-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(_contentRoot);
            File.WriteAllText(Path.Combine(_contentRoot, "CHANGELOG.md"), EnglishChangelog);
        }

        public void Dispose() => Directory.Delete(_contentRoot, recursive: true);

        private ChangelogService CreateService()
        {
            var env = new Mock<IHostEnvironment>();
            env.Setup(e => e.ContentRootPath).Returns(_contentRoot);
            return new ChangelogService(env.Object, NullLogger<ChangelogService>.Instance);
        }

        private void WriteCzechChangelog(string content) =>
            File.WriteAllText(Path.Combine(_contentRoot, "CHANGELOG.cs.md"), content);

        [Fact]
        public async Task GetLatest_English_ReturnsEntriesNewestFirst()
        {
            var entries = await CreateService().GetLatestChangelogEntries(10);

            Assert.Equal(["1.1.0", "1.0.0"], entries.Select(e => e.Version));
            Assert.Contains("Step timeline.", entries[0].RawHtml);
        }

        [Fact]
        public async Task GetLatest_Czech_ReturnsTranslatedEntries()
        {
            WriteCzechChangelog("""
                ## [1.1.0] - 2025-09-03
                ### Přidáno
                - Časová osa kroků.

                ## [1.0.0] - 2025-09-01
                ### Přidáno
                - První veřejné vydání.
                """);

            var entries = await CreateService().GetLatestChangelogEntries(10, "cs");

            Assert.Equal(["1.1.0", "1.0.0"], entries.Select(e => e.Version));
            Assert.Contains("Časová osa kroků.", entries[0].RawHtml);
            Assert.Contains("První veřejné vydání.", entries[1].RawHtml);
        }

        [Fact]
        public async Task GetLatest_Czech_FallsBackToEnglishForUntranslatedVersions()
        {
            WriteCzechChangelog("""
                ## [1.0.0] - 2025-09-01
                ### Přidáno
                - První veřejné vydání.
                """);

            var entries = await CreateService().GetLatestChangelogEntries(10, "cs");

            Assert.Equal(["1.1.0", "1.0.0"], entries.Select(e => e.Version));
            Assert.Contains("Step timeline.", entries[0].RawHtml);
            Assert.Contains("První veřejné vydání.", entries[1].RawHtml);
        }

        [Fact]
        public async Task GetLatest_Czech_MissingFile_ReturnsEnglish()
        {
            var entries = await CreateService().GetLatestChangelogEntries(10, "cs");

            Assert.Equal(2, entries.Count);
            Assert.Contains("Step timeline.", entries[0].RawHtml);
        }

        [Fact]
        public async Task GetLatest_UnknownLanguage_ReturnsEnglish()
        {
            var entries = await CreateService().GetLatestChangelogEntries(10, "de");

            Assert.Contains("Step timeline.", entries[0].RawHtml);
        }

        [Fact]
        public async Task GetLatest_RespectsCount()
        {
            var entries = await CreateService().GetLatestChangelogEntries(1);

            Assert.Single(entries);
            Assert.Equal("1.1.0", entries[0].Version);
        }

        [Fact]
        public async Task GetSinceVersion_ReturnsOnlyNewerEntries()
        {
            var entries = await CreateService().GetChangelogEntriesSinceVersion("1.0.0");

            Assert.Equal(["1.1.0"], entries.Select(e => e.Version));
        }

        [Fact]
        public async Task GetForVersion_Czech_ReturnsTranslation()
        {
            WriteCzechChangelog("""
                ## [1.0.0] - 2025-09-01
                ### Přidáno
                - První veřejné vydání.
                """);

            var entry = await CreateService().GetChangelogEntryForVersion("1.0.0", "cs");

            Assert.Contains("První veřejné vydání.", entry!.RawHtml);
        }

        [Fact]
        public async Task RealChangelogs_EveryCzechVersionExistsInEnglish()
        {
            // Guards against typos in CHANGELOG.cs.md version headers, which would silently fall back to English.
            var env = new Mock<IHostEnvironment>();
            env.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);
            var service = new ChangelogService(env.Object, NullLogger<ChangelogService>.Instance);

            var english = await service.GetLatestChangelogEntries(int.MaxValue);
            var czech = await service.GetLatestChangelogEntries(int.MaxValue, "cs");

            Assert.NotEmpty(english);
            Assert.Equal(english.Select(e => e.Version), czech.Select(e => e.Version));
            Assert.All(czech.Zip(english), pair => Assert.NotEqual(pair.Second.RawHtml, pair.First.RawHtml));
        }
    }
}
