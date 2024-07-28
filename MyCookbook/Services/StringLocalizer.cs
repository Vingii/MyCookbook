using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
        private readonly CultureProvider _cultureProvider;

        public CookbookStringLocalizer(IOptions<LocalizationOptions> localizationOptions, CultureProvider cultureProvider, ILogger<EmailSender> logger)
        {
            _localizationOptions = localizationOptions;
            _cultureProvider = cultureProvider;
            _logger = logger;
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            throw new NotImplementedException();
        }
        private LocalizedString FindLocalizedString(string key, object[]? arguments = default)
        {
            LocalizedString result;

            try
            {
                var resourceManager = CreateResourceManager();
                var resourceManagerShared = CreateResourceManager(true);
                string value;
                try
                {
                    value = resourceManager.GetString(key, _cultureProvider.SelectedCulture);
                }
                catch
                {
                    value = resourceManagerShared.GetString(key, _cultureProvider.SelectedCulture);
                }

                if (arguments is not null)
                {
                    value = string.Format(value, arguments);
                }

                result = new(key, value, false, GetResourceLocalization());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to localize string \"{key}\" with culture \"{_cultureProvider.SelectedCulture}\".", ex);
                result = new(key, "", true, GetResourceLocalization());
            }

            return result;
        }

        private ResourceManager CreateResourceManager(bool shared = false)
        {
            string resourceLocalization =  shared ? GetResourceLocalizationShared() : GetResourceLocalization();
            return new ResourceManager(resourceLocalization, Assembly.GetExecutingAssembly());
        }

        private string GetResourceLocalization()
        {
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
