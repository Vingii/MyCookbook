using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using MyCookbook.Components.Account;
using MyCookbook.Data;
using MyCookbook.Services;

namespace MyCookbook
{
    public static class ProgramExtensions
    {
        public static ISecretsProvider AddSecretsProvider(this IServiceCollection services, WebApplicationBuilder builder)
        {
            var secretsProvider = new SecretsProvider(builder);
            services.AddSingleton<ISecretsProvider>(secretsProvider);
            return secretsProvider;
        }

        public static void AddFeedbackProvider(this IServiceCollection services, ISecretsProvider secretsProvider, ConfigurationManager config)
        {
            var feedbackProvider = new CannyFeedbackProvider(secretsProvider.GetSecret("CannyKey"), config["CannyFeedbackUrl"], config["CannyBoardId"], config["CannyUserId"]);
            services.AddSingleton<IFeedbackProvider>(feedbackProvider);
        }

        public static void AddLanguageDictionary(this IServiceCollection services)
        {
            var dictionary = new MemoryLanguageDictionary("Static/Dictionaries");
            services.AddSingleton<ILanguageDictionary>(dictionary);
        }

        public static void AddCultureLocalization(this IServiceCollection services, ConfigurationManager config)
        {

            var cultureProvider = new CultureProvider("en", config.GetSection("SupportedCultures").Get<string[]>() ?? new string[] { "en" });
            services.AddLocalization(options => options.ResourcesPath = "LanguageResources");
            services.AddScoped<LanguageNotifier>();
            services.AddSingleton(cultureProvider);
            services.AddScoped(typeof(IStringLocalizer<>), typeof(CookbookStringLocalizer<>));
            services.Configure<RequestLocalizationOptions>(options =>
            {
                options.AddSupportedCultures(cultureProvider.SupportedCultures);
                options.AddSupportedUICultures(cultureProvider.SupportedCultures);
                options.RequestCultureProviders = new List<IRequestCultureProvider>()
                    {
                        cultureProvider
                    };
            });
        }
        public static void AddAuth(this IServiceCollection services, ISecretsProvider secretsProvider, bool isDev)
        {
            services.AddCascadingAuthenticationState();
            services.AddScoped<IdentityUserAccessor>();
            services.AddScoped<IdentityRedirectManager>();

            services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            Action<IdentityOptions> identityOptions = isDev
                ? options =>
                    {
                        options.Password.RequireDigit = false;
                        options.Password.RequireLowercase = false;
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequireUppercase = false;
                        options.Password.RequiredLength = 1;
                        options.Password.RequiredUniqueChars = 1;
                        options.SignIn.RequireConfirmedEmail = false;
                        options.SignIn.RequireConfirmedAccount = false;
                    }
                : options =>
                    {
                        options.Password.RequireDigit = false;
                        options.Password.RequireLowercase = false;
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequireUppercase = false;
                        options.Password.RequiredLength = 8;
                        options.Password.RequiredUniqueChars = 1;
                        options.SignIn.RequireConfirmedAccount = true;
                    };


            services.AddIdentity<ApplicationUser, IdentityRole>(identityOptions)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                })
               .AddGoogle(options =>
               {
                   options.ClientId = secretsProvider.GetSecret("Authentication:Google:ClientId");
                   options.ClientSecret = secretsProvider.GetSecret("Authentication:Google:ClientSecret");
               })
               .AddCookie("Cookies");

            if (!isDev)
            {
                services.AddTransient<IEmailSender, EmailSender>();
            }
            services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
        }
    }
}
