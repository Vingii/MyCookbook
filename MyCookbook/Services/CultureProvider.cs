using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace MyCookbook.Services
{
    public class CultureProvider : RequestCultureProvider
    {
        public string SelectedLanguage { get; private set; }
        public CultureInfo SelectedCulture { get; set; }
        public string DefaultCulture { get; set; }

        public CultureProvider(string defaultCulture)
        {
            DefaultCulture = defaultCulture;
        }

        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            string inputCulture = httpContext.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName] ?? "";
            var result = CookieRequestCultureProvider.ParseCookieValue(inputCulture);

            if (result is null)
            {
                SelectedLanguage = DefaultCulture;
                result = new(DefaultCulture);
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
