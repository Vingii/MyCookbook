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

        private static void SetDisplayName(ClaimsIdentity identity, string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return;
            var existing = identity.FindFirst(ClaimTypes.GivenName);
            if (existing != null) identity.RemoveClaim(existing);
            identity.AddClaim(new Claim(ClaimTypes.GivenName, displayName));
        }

        private static string? UserInfoString(System.Text.Json.JsonElement userInfo, string key) =>
            userInfo.TryGetProperty(key, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
                ? value.GetString()
                : null;

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
                    // Authentik may leave profile claims (preferred_username, name) out of the ID token.
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.Events = new OpenIdConnectEvents
                    {
                        OnTokenValidated = ctx =>
                        {
                            // Map OIDC `sub` claim → ClaimTypes.Name for DB-key compatibility
                            // (previously set from X-Authentik-Uid, which equals the OIDC `sub`).
                            var sub = ctx.Principal!.FindFirst("sub")?.Value;
                            if (sub == null) return Task.CompletedTask;

                            var identity = (ClaimsIdentity)ctx.Principal.Identity!;
                            var existingName = identity.FindFirst(ClaimTypes.Name);
                            if (existingName != null) identity.RemoveClaim(existingName);
                            identity.AddClaim(new Claim(ClaimTypes.Name, sub));

                            SetDisplayName(identity, ctx.Principal.FindFirst("preferred_username")?.Value
                                ?? ctx.Principal.FindFirst("name")?.Value);
                            return Task.CompletedTask;
                        },
                        OnUserInformationReceived = ctx =>
                        {
                            var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                            if (identity.HasClaim(c => c.Type == ClaimTypes.GivenName)) return Task.CompletedTask;
                            SetDisplayName(identity, UserInfoString(ctx.User.RootElement, "preferred_username")
                                ?? UserInfoString(ctx.User.RootElement, "name"));
                            return Task.CompletedTask;
                        },
                        // Store the display name right at sign-in, so other users see it (e.g. on the Shared page).
                        OnTicketReceived = async ctx =>
                        {
                            var uid = ctx.Principal?.Identity?.Name;
                            var displayName = ctx.Principal?.FindFirst(ClaimTypes.GivenName)?.Value;
                            if (uid == null || displayName == null) return;
                            try
                            {
                                var db = ctx.HttpContext.RequestServices.GetRequiredService<Data.CookbookDatabaseService>();
                                await db.UpdateUserPreference("DisplayName", displayName, uid);
                            }
                            catch (Exception ex)
                            {
                                // Never fail the sign-in over this; the per-request update retries it.
                                Serilog.Log.Warning(ex, "Could not store display name for {User}", uid);
                            }
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
