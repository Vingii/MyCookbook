using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using MyCookbook.Logging;
using System.Reflection;
using System.Resources;

namespace MyCookbook.Services
{
    public class CookbookStringLocalizer<TComponent> : IStringLocalizer<TComponent>
    {
        public LocalizedString this[string name] => FindLocalizedString(name);
        public LocalizedString this[string name, params object[] arguments] => FindLocalizedString(name, arguments);

        private readonly ILogger _logger;
        private readonly IOptions<LocalizationOptions> _localizationOptions;
        private readonly UserSettings _userSettings;

        public CookbookStringLocalizer(IOptions<LocalizationOptions> localizationOptions, UserSettings userSettings, ILogger<MailgunEmailSender> logger)
        {
            using var timeLogger = new TimeLogger(MethodBase.GetCurrentMethod());
            _localizationOptions = localizationOptions;
            _userSettings = userSettings;
            _logger = logger;
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            throw new NotImplementedException();
        }
        private LocalizedString FindLocalizedString(string key, object[]? arguments = default)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            LocalizedString result;

            try
            {
                var resourceManager = CreateResourceManager();
                var resourceManagerShared = CreateResourceManager(true);
                string value;
                try
                {
                    value = resourceManager.GetString(key, _userSettings.Culture);
                }
                catch
                {
                    value = resourceManagerShared.GetString(key, _userSettings.Culture);
                }

                if (arguments is not null)
                {
                    value = string.Format(value, arguments);
                }

                result = new(key, value, false, GetResourceLocalization());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to localize string \"{key}\" with culture \"{_userSettings.Culture}\".", ex);
                result = new(key, key, true, GetResourceLocalization());
            }

            return result;
        }

        private ResourceManager CreateResourceManager(bool shared = false)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            string resourceLocalization =  shared ? GetResourceLocalizationShared() : GetResourceLocalization();
            return new ResourceManager(resourceLocalization, typeof(CookbookStringLocalizer<>).Assembly);
        }

        private string GetResourceLocalization()
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var componentType = typeof(TComponent);
            var nameParts = componentType.FullName.Split('.').ToList();
            nameParts.Insert(1, _localizationOptions.Value.ResourcesPath);
            return string.Join(".", nameParts);
        }

        private string GetResourceLocalizationShared()
        {
            return $"MyCookbook.{_localizationOptions.Value.ResourcesPath}.Shared";
        }
    }
}
