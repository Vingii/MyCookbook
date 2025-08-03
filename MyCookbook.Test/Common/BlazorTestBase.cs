using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using MyCookbook.Data;
using MyCookbook.Services;
using System.Security.Claims;

namespace MyCookbook.Test.Common
{
    public class BlazorTestBase : TestContext, IClassFixture<TestingWebAppFactory<Program>>, IAsyncLifetime
    {
        protected readonly Mock<IDialogService> _dialogServiceMock;
        public BlazorTestBase(TestingWebAppFactory<Program> factory)
        {
            JSInterop.SetupVoid("mudPopover.initialize", _ => true);
            JSInterop.SetupVoid("mudPopover.connect", _ => true);
            JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
            JSInterop.SetupVoid("mudScrollManager.unlockScroll", _ => true);

            _dialogServiceMock = new Mock<IDialogService>();

            var realServices = factory.Services;

            var testUser = "test-user";
            var authContext = this.AddTestAuthorization();
            authContext.SetAuthorized(testUser);
            authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, testUser));

            Services.AddFallbackServiceProvider(factory.Services);
            Services.AddMudServices();
            Services.AddSingleton(realServices.GetRequiredService<CookbookDatabaseService>());
            Services.AddSingleton(realServices.GetRequiredService<CultureProvider>());
            Services.AddSingleton(realServices.GetRequiredService<ILoggerFactory>());
            Services.AddSingleton(realServices.GetRequiredService<IOptions<LocalizationOptions>>());
            Services.AddSingleton(_dialogServiceMock.Object);
            Services.AddScoped<LanguageNotifier>();
            Services.AddScoped(typeof(IStringLocalizer<>), typeof(CookbookStringLocalizer<>));
        }

        public async Task InitializeAsync()
        {
            using var context = await GetDbService().GetContext();
            await context.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            using var context = await GetDbService().GetContext();
            await context.Database.EnsureDeletedAsync();
        }

        protected CookbookDatabaseService GetDbService() => Services.GetRequiredService<CookbookDatabaseService>();
        protected FakeNavigationManager GetNavManager() => Services.GetRequiredService<FakeNavigationManager>();
    }
}