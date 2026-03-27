using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MyCookbook.Services;

public class HeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? userId = null;
        string? userName = null;

        if (Request.Headers.TryGetValue("X-Authentik-Uid", out var uidValue) && !string.IsNullOrEmpty(uidValue))
        {
            userId = uidValue.ToString();

            if ((Request.Headers.TryGetValue("X-Authentik-Name", out var nameValue) && nameValue != "")
                || Request.Headers.TryGetValue("X-Authentik-Username", out nameValue))
            {
                userName = nameValue.ToString();
            }
        }
        else if (config["DEV_AUTO_LOGIN"] is { Length: > 0 } devUser)
        {
            userId = devUser;
            userName = devUser;
        }

        if (userId == null)
            return Task.FromResult(AuthenticateResult.NoResult());

        // ClaimTypes.Name is used as UserName throughout the app and must match
        // what was stored in the DB by the old Blazor app (X-Authentik-Uid).
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId),
        };
        if (userName != null)
            claims.Add(new Claim(ClaimTypes.GivenName, userName));
        if (Request.Headers.TryGetValue("X-Authentik-Email", out var email))
            claims.Add(new Claim(ClaimTypes.Email, email.ToString()));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
