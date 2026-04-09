using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;

namespace MyCookbook.Services;

// Intercepts non-API requests that carry a ?token= query parameter.
// Validates the API token, signs the user in via an HttpOnly session cookie,
// then redirects to the same URL without the token in the query string.
//
// This lets Home Assistant dashboards link with an embedded token
// (e.g. https://cookbook.example.com/?token=<api-token>) without exposing
// the raw token in localStorage or JavaScript at all.
public class TokenQueryParamMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ApiTokenService tokenService)
    {
        if (!context.Request.Path.StartsWithSegments("/api")
            && context.Request.Query.TryGetValue("token", out var tokenValues)
            && tokenValues.FirstOrDefault() is { Length: > 0 } rawToken)
        {
            var username = await tokenService.LookupUserByTokenAsync(rawToken);
            if (username != null)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.NameIdentifier, username),
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(3),
                    });

                var query = QueryHelpers.ParseQuery(context.Request.QueryString.Value);
                query.Remove("token");
                var newQuery = QueryString.Create(
                    query.SelectMany(kv => kv.Value.Select(v => KeyValuePair.Create<string, string?>(kv.Key, v))));
                context.Response.Redirect(context.Request.Path + newQuery);
                return;
            }
        }

        await next(context);
    }
}
