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
        public static void AddFeedbackProvider(this IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<IFeedbackProvider>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var client = httpClientFactory.CreateClient();

                return new JiraFeedbackProvider(client, config["Jira:Domain"], config["Jira:Email"], config["Jira:Key"], config["Jira:ProjectKey"]);
            });
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
        
        public static void AddAuth(this IServiceCollection services, IConfiguration config, bool isDev)
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
               .AddCookie("Cookies");

            if (isDev)
            {
                services.AddTransient<IEmailSender, MailgunEmailSender>();
            }
            services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
        }
    }
}
