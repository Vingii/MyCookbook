using MyCookbook.Services;

namespace MyCookbook.Test.Services
{
    public class CultureProviderTests
    {
        [Fact]
        public void Constructor_WhenInitialized_SetsPropertiesCorrectly()
        {
            // Arrange
            var supportedCultures = new[] { "en", "pl-PL", "de-DE" };
            var defaultCulture = "en";

            // Act
            var provider = new CultureProvider(defaultCulture, supportedCultures);

            // Assert
            Assert.Equal(defaultCulture, provider.DefaultCulture);
            Assert.Equal(supportedCultures, provider.SupportedCultures);
            Assert.Equal(3, provider.SupportedLanguages.Count);
            Assert.Equal("English", provider.SupportedLanguages["en"]);
            Assert.Equal("Polski", provider.SupportedLanguages["pl-PL"]);
            Assert.Equal("Deutsch", provider.SupportedLanguages["de-DE"]);
        }
    }
}