using Moq;
using MyCookbook.Services;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Localization;

namespace MyCookbook.Test.Services
{
    public class CultureProviderTests
    {
        private readonly string[] _supportedCultures = { "en", "pl-PL", "de" };
        private readonly string _defaultCulture = "en";

        private Mock<HttpContext> CreateMockHttpContext(string? cookieValue, string? acceptLanguageHeader)
        {
            var mockRequest = new Mock<HttpRequest>();
            var mockHttpContext = new Mock<HttpContext>();
            var headers = new HeaderDictionary();

            // Setup Cookies
            var cookies = new Mock<IRequestCookieCollection>();
            string outCookie;
            cookies.Setup(c => c.TryGetValue(CookieRequestCultureProvider.DefaultCookieName, out outCookie))
                   .Returns(cookieValue != null);
            mockRequest
                .SetupGet(r => r.Cookies[CookieRequestCultureProvider.DefaultCookieName])
                .Returns(cookieValue);

            // Setup Headers
            if (acceptLanguageHeader != null)
            {
                headers.Add(HeaderNames.AcceptLanguage, acceptLanguageHeader);
            }
            mockRequest.Setup(r => r.Headers).Returns(headers);

            mockHttpContext.Setup(c => c.Request).Returns(mockRequest.Object);
            return mockHttpContext;
        }

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

        [Fact]
        public async Task DetermineProviderCultureResult_WithValidCookie_ReturnsCultureFromCookie()
        {
            // Arrange
            var provider = new CultureProvider(_defaultCulture, _supportedCultures);
            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture("pl-PL"));
            var mockHttpContext = CreateMockHttpContext(cookieValue, null);

            // Act
            var result = await provider.DetermineProviderCultureResult(mockHttpContext.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("pl-PL", result.Cultures.First().Value);
            Assert.Equal("pl-PL", provider.SelectedLanguage);
            Assert.Equal("pl-PL", provider.SelectedCulture.Name);
        }

        [Fact]
        public async Task DetermineProviderCultureResult_WithNoCookie_UsesHighestQualityAcceptLanguageHeader()
        {
            // Arrange
            var provider = new CultureProvider(_defaultCulture, _supportedCultures);
            var mockHttpContext = CreateMockHttpContext(null, "fr-CH, fr;q=0.9, en;q=0.8, de;q=0.9, *;q=0.5");

            // Act
            var result = await provider.DetermineProviderCultureResult(mockHttpContext.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("de", result.Cultures.First().Value);
            Assert.Equal("de", provider.SelectedLanguage);
            Assert.Equal("de", provider.SelectedCulture.Name);
        }

        [Fact]
        public async Task DetermineProviderCultureResult_WithUnsupportedAcceptLanguageHeader_FallsBackToDefaultCulture()
        {
            // Arrange
            var provider = new CultureProvider(_defaultCulture, _supportedCultures);
            var mockHttpContext = CreateMockHttpContext(null, "es-ES,es;q=0.9");

            // Act
            var result = await provider.DetermineProviderCultureResult(mockHttpContext.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_defaultCulture, result.Cultures.First().Value);
            Assert.Equal(_defaultCulture, provider.SelectedLanguage);
        }

        [Fact]
        public async Task DetermineProviderCultureResult_WithNoCookieAndNoHeaders_FallsBackToDefaultCulture()
        {
            // Arrange
            var provider = new CultureProvider(_defaultCulture, _supportedCultures);
            var mockHttpContext = CreateMockHttpContext(null, null);

            // Act
            var result = await provider.DetermineProviderCultureResult(mockHttpContext.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_defaultCulture, result.Cultures.First().Value);
            Assert.Equal(_defaultCulture, provider.SelectedLanguage);
        }

        [Fact]
        public async Task DetermineProviderCultureResult_WhenCookieIsMalformed_FallsBackToAcceptLanguageHeader()
        {
            // Arrange
            var provider = new CultureProvider(_defaultCulture, _supportedCultures);
            var mockHttpContext = CreateMockHttpContext("malformed-cookie", "pl-PL");

            // Act
            var result = await provider.DetermineProviderCultureResult(mockHttpContext.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("pl-PL", result.Cultures.First().Value);
            Assert.Equal("pl-PL", provider.SelectedLanguage);
        }

        [Fact]
        public async Task DetermineProviderCultureResult_SetsCurrentThreadCulture()
        {
            // Arrange
            var initialCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentCulture = initialCulture;
            CultureInfo.CurrentUICulture = initialCulture;

            var provider = new CultureProvider(_defaultCulture, _supportedCultures);
            var mockHttpContext = CreateMockHttpContext(null, "pl-PL");

            // Act
            await provider.DetermineProviderCultureResult(mockHttpContext.Object);

            // Assert
            Assert.Equal("pl-PL", CultureInfo.CurrentCulture.Name);
            Assert.Equal("pl-PL", CultureInfo.CurrentUICulture.Name);
        }
    }
}