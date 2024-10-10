using Microsoft.AspNetCore.Components;
using MyCookbook.Logging;
using System.Reflection;

namespace MyCookbook.Services
{
    public class LanguageNotifier
    {
        private readonly List<ComponentBase> _subscribedComponents = new();

        public void SubscribeLanguageChange(ComponentBase component) => _subscribedComponents.Add(component);

        public void UnsubscribeLanguageChange(ComponentBase component) => _subscribedComponents.Remove(component);

        public void NotifyLanguageChange()
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            foreach (var component in _subscribedComponents)
            {
                if (component is not null)
                {
                    var stateHasChangedMethod = component.GetType()?.GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                    _ = (stateHasChangedMethod?.Invoke(component, null));
                }
            }
        }
    }
}
