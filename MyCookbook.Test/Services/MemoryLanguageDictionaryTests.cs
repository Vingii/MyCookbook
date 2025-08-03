using Moq;
using MyCookbook.Services;
using Microsoft.AspNetCore.Hosting;
using MyCookbook.Model;
using Newtonsoft.Json;

namespace MyCookbook.Test.Services
{
    public class MemoryLanguageDictionaryTests : IDisposable
    {
        private readonly string _tempRootPath;
        private readonly string _tempDictionaryPath;
        private readonly Mock<IWebHostEnvironment> _mockWebHostEnvironment;

        public MemoryLanguageDictionaryTests()
        {
            _tempRootPath = Path.Combine(Path.GetTempPath(), "MyCookbookTests", Guid.NewGuid().ToString());
            _tempDictionaryPath = Path.Combine(_tempRootPath, "Static", "Dictionaries");
            Directory.CreateDirectory(_tempDictionaryPath);

            _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
            _mockWebHostEnvironment.Setup(e => e.WebRootPath).Returns(_tempRootPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRootPath))
            {
                Directory.Delete(_tempRootPath, true);
            }
        }

        private void CreateFakeDictionaryFile(string fileName, params WordInflections[] words)
        {
            var lines = words.Select(JsonConvert.SerializeObject);
            File.WriteAllLines(Path.Combine(_tempDictionaryPath, fileName), lines);
        }

        [Fact]
        public void Constructor_WhenDictionaryIsValid_LoadsInflectionsCorrectly()
        {
            // Arrange
            CreateFakeDictionaryFile("test.json", new WordInflections
            {
                Word = "run",
                Inflections = { "run", "ran", "running" }
            });

            // Act
            var dictionary = new MemoryLanguageDictionary(_mockWebHostEnvironment.Object);
            var result = dictionary.WordInflections("ran");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
            Assert.Equal(new[] { "running", "run", "ran" }, result.OrderByDescending(x => x.Length));
        }

        [Fact]
        public void WordInflections_WhenCalledForAllInflections_ReturnsSameCollection()
        {
            // Arrange
            CreateFakeDictionaryFile("test.json", new WordInflections
            {
                Word = "eat",
                Inflections = { "eat", "ate", "eaten" }
            });
            var dictionary = new MemoryLanguageDictionary(_mockWebHostEnvironment.Object);

            // Act
            var resultForEat = dictionary.WordInflections("eat");
            var resultForAte = dictionary.WordInflections("ate");
            var resultForEaten = dictionary.WordInflections("eaten");

            // Assert
            Assert.Equal(resultForEat, resultForAte);
            Assert.Equal(resultForAte, resultForEaten);
        }

        [Fact]
        public void WordInflections_WhenCalled_IsCaseInsensitive()
        {
            // Arrange
            CreateFakeDictionaryFile("test.json", new WordInflections
            {
                Word = "Case",
                Inflections = { "Case", "Casing" }
            });
            var dictionary = new MemoryLanguageDictionary(_mockWebHostEnvironment.Object);

            // Act
            var result = dictionary.WordInflections("cAsInG");

            // Assert
            Assert.Contains("casing", result);
            Assert.Contains("case", result);
        }

        [Fact]
        public void WordInflections_ForWordNotInDictionary_ReturnsEmptyList()
        {
            // Arrange
            CreateFakeDictionaryFile("test.json", new WordInflections
            {
                Word = "existing",
                Inflections = { "existing" }
            });
            var dictionary = new MemoryLanguageDictionary(_mockWebHostEnvironment.Object);

            // Act
            var result = dictionary.WordInflections("nonexistent");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Constructor_HandlesMalformedJsonLinesGracefully()
        {
            // Arrange
            var filePath = Path.Combine(_tempDictionaryPath, "malformed.json");
            File.WriteAllText(filePath,
                "{\"word\":\"good\",\"inflections\":[\"good\"]}\n" +
                "this is not json\n" +
                "{\"word\":\"also-good\",\"inflections\":[\"also-good\"]}"
            );

            // Act
            var dictionary = new MemoryLanguageDictionary(_mockWebHostEnvironment.Object);
            var goodResult = dictionary.WordInflections("good");
            var alsoGoodResult = dictionary.WordInflections("also-good");

            // Assert
            Assert.Single(goodResult);
            Assert.Single(alsoGoodResult);
        }
    }
}