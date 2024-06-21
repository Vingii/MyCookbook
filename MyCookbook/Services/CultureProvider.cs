using Microsoft.AspNetCore.Localization;
using MyCookbook.Utils;
using System.Globalization;

namespace MyCookbook.Services
{
    public class CultureProvider : RequestCultureProvider
    {
        public string[] SupportedCultures { get; }
        public Dictionary<string, string> SupportedLanguages { get; }

        public string SelectedLanguage { get; private set; }
        public CultureInfo SelectedCulture { get; set; }
        public string DefaultCulture { get; set; }

        public CultureProvider(string defaultCulture, string[] supportedCultures)
        {
            DefaultCulture = defaultCulture;
            SupportedCultures = supportedCultures;
            SupportedLanguages = supportedCultures.ToDictionary(x => x,
                x => CultureInfo.GetCultureInfo(x).IsNeutralCulture
                    ? CultureInfo.GetCultureInfo(x).NativeName.CapitalizeFirst()
                    : CultureInfo.GetCultureInfo(x).Parent.NativeName.CapitalizeFirst());
        }

        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            string inputCulture = httpContext.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName] ?? "";
            var result = CookieRequestCultureProvider.ParseCookieValue(inputCulture);

            if (result is null)
            {
                SelectedLanguage = httpContext.Request.GetTypedHeaders()
                           .AcceptLanguage
                           ?.OrderByDescending(x => x.Quality ?? 1)
                           .FirstOrDefault()?.Value.ToString() ?? DefaultCulture;

                if (!SupportedCultures.Contains(SelectedLanguage))
                {
                    SelectedLanguage = DefaultCulture;
                }

                result = new(SelectedLanguage);
            }
            else
            {
                SelectedLanguage = result.Cultures.First().Value;
            }

            SelectedCulture = CultureInfo.GetCultureInfo(SelectedLanguage);
            CultureInfo.CurrentCulture = SelectedCulture;
            CultureInfo.CurrentUICulture = SelectedCulture;

            return Task.FromResult<ProviderCultureResult?>(result);
        }
    }
}
