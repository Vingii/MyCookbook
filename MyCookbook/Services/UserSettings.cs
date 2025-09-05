using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using MyCookbook.Data;
using System.Globalization;
using System.Net.Http;
using System.Security.Claims;

namespace MyCookbook.Services
{
    public class UserSettings(CookbookDatabaseService dbService, IHttpContextAccessor httpContextAccessor, CultureProvider cultureProvider)
    {
        private readonly CookbookDatabaseService _dbService = dbService;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly CultureProvider _cultureProvider = cultureProvider;

        public string? _language;
        public CultureInfo? _culture;
        public string? _username;

        public CultureInfo Culture
        {
            get
            {
                return _culture ?? CultureInfo.GetCultureInfo(_cultureProvider.DefaultCulture);
            }
        }

        public string Username 
        {
            get
            {
                _username ??= GetUserIdentityName();
                return _username;
            }
        }

        public bool IsAuthenticated => Username != "";

        public async Task<string> GetLanguage()
        {
            if (_language == null)
            {
                if (IsAuthenticated)
                {
                    _language ??= await _dbService.GetUserPreference("Language", Username);
                }
                else
                {
                    string inputCulture = _httpContextAccessor.HttpContext?.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName] ?? "";
                    var result = CookieRequestCultureProvider.ParseCookieValue(inputCulture);

                    string? language = null;
                    if (result is null)
                    {
                        language = _httpContextAccessor.HttpContext?.Request.GetTypedHeaders()
                                   .AcceptLanguage
                                   ?.OrderByDescending(x => x.Quality ?? 1)
                                   .FirstOrDefault(x => _cultureProvider.SupportedCultures.Contains(x.Value.ToString()))?.Value.ToString() ?? _cultureProvider.DefaultCulture;

                    }
                    else
                    {
                        language = result.Cultures.First().Value;
                    }

                    _language = language;
                }

                _culture ??= CultureInfo.GetCultureInfo(_language ?? _cultureProvider.DefaultCulture);
            }

            return _language ?? _cultureProvider.DefaultCulture;
        }

        public async Task SetLanguage(string value)
        {
            _language = value;
            _culture = CultureInfo.GetCultureInfo(value);
            await _dbService.UpdateUserPreference("Language", value, Username);
        }

        public async Task<CultureInfo> GetCulture()
        {
            _culture ??= CultureInfo.GetCultureInfo(await GetLanguage());
            return _culture;
        }

        private string GetUserIdentityName()
        {
            string userIdentityName = "";

            var user = _httpContextAccessor.HttpContext?.User;

            if (user != null && user.Identity != null && user.Identity.IsAuthenticated)
            {
                var nameClaim = user.FindFirst(ClaimTypes.Name);

                if (nameClaim != null && !nameClaim.Value.StartsWith("guest-", StringComparison.InvariantCultureIgnoreCase))
                {
                    userIdentityName = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
                }
            }

            return userIdentityName;
        }
    }
}
