using Microsoft.AspNetCore.Localization;
using MyCookbook.Logging;
using MyCookbook.Utils;
using System.Globalization;
using System.Reflection;

namespace MyCookbook.Services
{
    public class CultureProvider : RequestCultureProvider
    {
        public string[] SupportedCultures { get; }
        public Dictionary<string, string> SupportedLanguages { get; }

        public string DefaultCulture { get; set; }

        public CultureProvider(string defaultCulture, string[] supportedCultures)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            DefaultCulture = defaultCulture;
            SupportedCultures = supportedCultures;
            SupportedLanguages = supportedCultures.ToDictionary(x => x,
                x => CultureInfo.GetCultureInfo(x).IsNeutralCulture
                    ? CultureInfo.GetCultureInfo(x).NativeName.CapitalizeFirst()
                    : CultureInfo.GetCultureInfo(x).Parent.NativeName.CapitalizeFirst());
        }

        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            string inputCulture = httpContext.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName] ?? "";
            var result = CookieRequestCultureProvider.ParseCookieValue(inputCulture);

            if (result is null)
            {
                var language = httpContext.Request.GetTypedHeaders()
                           .AcceptLanguage
                           ?.OrderByDescending(x => x.Quality ?? 1)
                           .FirstOrDefault(x => SupportedCultures.Contains(x.Value.ToString()))?.Value.ToString() ?? DefaultCulture;

                result = new(language);
            }
            else
            {
                var language = result.Cultures.First().Value;
                result = new(language);
            }

            return Task.FromResult<ProviderCultureResult?>(result);
        }
    }
}
