using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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

                var channel = config["Cookbook:Channel"];
                return new YouTrackFeedbackProvider(client, config["YouTrack:BaseUrl"], config["YouTrack:Token"], config["YouTrack:ProjectId"], channel: channel);
            });
        }

        public static void AddApiKeyAuth(this IServiceCollection services, IConfiguration config)
        {
            var isDev = config["DEV_AUTO_LOGIN"] is { Length: > 0 };

            services.AddTransient<ApiTokenService>();

            var authBuilder = services.AddAuthentication("Default")
                .AddPolicyScheme("Default", "Default", options =>
                {
                    options.ForwardDefaultSelector = ctx =>
                        ctx.Request.Headers.ContainsKey("Authorization")
                            ? "ApiKey"
                            : isDev ? "DevAutoLogin" : CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.ExpireTimeSpan = TimeSpan.FromDays(30);
                    options.SlidingExpiration = true;
                    options.LoginPath = "/api/auth/login";
                    options.AccessDeniedPath = "/unauthorized";
                })
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });

            if (isDev)
            {
                authBuilder.AddScheme<AuthenticationSchemeOptions, DevAutoLoginHandler>("DevAutoLogin", _ => { });
            }
            else
            {
                var authority = config["Authentik:Authority"]
                    ?? throw new InvalidOperationException("Authentik:Authority is not configured.");
                var clientId = config["Authentik:ClientId"]
                    ?? throw new InvalidOperationException("Authentik:ClientId is not configured.");
                var clientSecret = config["Authentik:ClientSecret"]
                    ?? throw new InvalidOperationException("Authentik:ClientSecret is not configured.");

                authBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = authority;
                    options.ClientId = clientId;
                    options.ClientSecret = clientSecret;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.SaveTokens = true;
                    options.MapInboundClaims = false;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.Events = new OpenIdConnectEvents
                    {
                        OnTokenValidated = ctx =>
                        {
                            // Map OIDC `sub` claim → ClaimTypes.Name for DB-key compatibility
                            // (previously set from X-Authentik-Uid, which equals the OIDC `sub`).
                            var sub = ctx.Principal!.FindFirst("sub")?.Value;
                            var displayName = ctx.Principal.FindFirst("preferred_username")?.Value
                                ?? ctx.Principal.FindFirst("name")?.Value;

                            if (sub == null) return Task.CompletedTask;

                            var identity = (ClaimsIdentity)ctx.Principal.Identity!;
                            var existingName = identity.FindFirst(ClaimTypes.Name);
                            if (existingName != null) identity.RemoveClaim(existingName);
                            identity.AddClaim(new Claim(ClaimTypes.Name, sub));

                            if (displayName != null)
                            {
                                var existingGivenName = identity.FindFirst(ClaimTypes.GivenName);
                                if (existingGivenName != null) identity.RemoveClaim(existingGivenName);
                                identity.AddClaim(new Claim(ClaimTypes.GivenName, displayName));
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
            }

            var webScheme = isDev ? "DevAutoLogin" : CookieAuthenticationDefaults.AuthenticationScheme;

            services.AddAuthorizationBuilder()
                .AddPolicy("CookieOrApiKey", policy =>
                    policy.RequireAuthenticatedUser()
                          .AddAuthenticationSchemes(webScheme, "ApiKey"))
                .AddPolicy("NotGuest", policy =>
                    policy.RequireAuthenticatedUser()
                          .AddAuthenticationSchemes(webScheme, "ApiKey")
                          .RequireAssertion(ctx =>
                              ctx.User.Identity?.Name?.StartsWith("guest-", StringComparison.OrdinalIgnoreCase) != true));
        }
    }
}
