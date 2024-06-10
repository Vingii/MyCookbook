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

        private readonly IOptions<LocalizationOptions> _localizationOptions;

        public CookbookStringLocalizer(IOptions<LocalizationOptions> localizationOptions)
        {
            _localizationOptions = localizationOptions;
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            throw new NotImplementedException();
        }
        private LocalizedString FindLocalizedString(string key, object[]? arguments = default)
        {
            var resourceManager = CreateResourceManager();
            LocalizedString result;

            try
            {
                string value = resourceManager.GetString(key);

                if (arguments is not null)
                {
                    value = string.Format(value, arguments);
                }

                result = new(key, value, false, GetResourceLocalization());
            }
            catch
            {
                result = new(key, "", true, GetResourceLocalization());
            }

            return result;
        }

        private ResourceManager CreateResourceManager()
        {
            string resourceLocaltion = GetResourceLocalization();
            var resourceManager = new ResourceManager(resourceLocaltion, Assembly.GetExecutingAssembly());

            return resourceManager;
        }

        private string GetResourceLocalization()
        {
            var componentType = typeof(TComponent);
            var nameParts = componentType.FullName.Split('.').ToList();
            nameParts.Insert(1, _localizationOptions.Value.ResourcesPath);
            return string.Join(".", nameParts);
        }
    }
}
