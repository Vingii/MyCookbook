using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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

                return new YouTrackFeedbackProvider(client, config["YouTrack:BaseUrl"], config["YouTrack:Token"], config["YouTrack:ProjectId"]);
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

        public static void AddApiKeyAuth(this IServiceCollection services)
        {
            services.AddTransient<ApiTokenService>();
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });

            services.AddAuthorizationBuilder()
                .AddPolicy("CookieOrApiKey", policy =>
                    policy.RequireAuthenticatedUser()
                          .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme, "ApiKey"));
        }
    }
}
