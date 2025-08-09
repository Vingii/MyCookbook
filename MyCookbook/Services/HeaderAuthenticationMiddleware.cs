using System.Security.Claims;

public class HeaderAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public HeaderAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Authentik-Uid", out var userId) &&
            !string.IsNullOrEmpty(userId))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };

            if ((context.Request.Headers.TryGetValue("X-Authentik-Name", out var userName) && userName != "")
                || context.Request.Headers.TryGetValue("X-Authentik-Username", out userName))
            {
                claims.Add(new Claim(ClaimTypes.Name, userName.ToString()));
            }

            if (context.Request.Headers.TryGetValue("X-Authentik-Email", out var email))
            {
                claims.Add(new Claim(ClaimTypes.Email, email.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "HeaderAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }
}