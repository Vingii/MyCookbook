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
        if (context.Request.Headers.TryGetValue("X-authentik-username", out var userName))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName.ToString()),
            };

            if (context.Request.Headers.TryGetValue("X-authentik-email", out var email))
            {
                claims.Add(new Claim(ClaimTypes.Email, email.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "HeaderAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }
}