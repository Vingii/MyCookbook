using Microsoft.AspNetCore.Authentication;
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

        public static void AddApiKeyAuth(this IServiceCollection services)
        {
            services.AddTransient<ApiTokenService>();
            services.AddAuthentication("HeaderAuth")
                .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("HeaderAuth", _ => { })
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });

            services.AddAuthorizationBuilder()
                .AddPolicy("CookieOrApiKey", policy =>
                    policy.RequireAuthenticatedUser()
                          .AddAuthenticationSchemes("HeaderAuth", "ApiKey"));
        }
    }
}
