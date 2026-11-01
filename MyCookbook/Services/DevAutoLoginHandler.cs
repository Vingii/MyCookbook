using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MyCookbook.Services;

// Development-only authentication handler: automatically signs in as the user
// specified by the DEV_AUTO_LOGIN config key, bypassing OIDC entirely.
// An X-Dev-User request header signs in as that user instead; E2E tests use it to run as
// their own user, so their cleanup never touches the developer's DEV_AUTO_LOGIN data.
// This grants nothing new: without DEV_AUTO_LOGIN the handler does nothing at all.
public class DevAutoLoginHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (config["DEV_AUTO_LOGIN"] is not { Length: > 0 } devUser)
            return Task.FromResult(AuthenticateResult.NoResult());

        if (Request.Headers["X-Dev-User"].FirstOrDefault() is { Length: > 0 } headerUser)
            devUser = headerUser;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, devUser),
            new Claim(ClaimTypes.Name, devUser),
            new Claim(ClaimTypes.GivenName, devUser),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
