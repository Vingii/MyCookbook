using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace MyCookbook.Services
{
    public class CultureProvider : RequestCultureProvider
    {
        public string SelectedLanguage { get; private set; }
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
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(DefaultCulture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(DefaultCulture);
                result = new(DefaultCulture);
            }
            else
            {
                SelectedLanguage = result.Cultures.First().Value;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(result.Cultures.First().Value);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(result.UICultures.First().Value);
            }

            return Task.FromResult<ProviderCultureResult?>(result);
        }
    }
}
