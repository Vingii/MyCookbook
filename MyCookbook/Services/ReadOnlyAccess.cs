using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace MyCookbook.Services;

/// <summary>
/// API tokens (Home Assistant) give read-only access: a Bearer token, or the browser session a <c>?token=</c>
/// link signs in, can view recipes and plans but not change anything. Such identities carry
/// <see cref="ClaimType"/>; <see cref="ReadOnlyAccessHandler"/> enforces it on every policy.
/// </summary>
public static class ReadOnlyAccess
{
    public const string ClaimType = "mycookbook:access";
    public const string ReadOnly = "read-only";

    public static Claim Claim() => new(ClaimType, ReadOnly);

    public static bool IsReadOnly(this ClaimsPrincipal user) =>
        user.HasClaim(ClaimType, ReadOnly);
}

/// <summary>Lets a read-only identity call this non-GET endpoint (e.g. remembering the last seen version).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowReadOnlyAccessAttribute : Attribute;

/// <summary>Keeps a read-only identity out of this GET endpoint (account secrets, or a GET with side effects).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DenyReadOnlyAccessAttribute : Attribute;

public sealed class ReadOnlyAccessRequirement : IAuthorizationRequirement;

public sealed class ReadOnlyAccessHandler : AuthorizationHandler<ReadOnlyAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ReadOnlyAccessRequirement requirement)
    {
        if (!context.User.IsReadOnly() || IsAllowed(context.Resource as HttpContext))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static bool IsAllowed(HttpContext? http)
    {
        if (http == null) return false;
        var metadata = http.GetEndpoint()?.Metadata;
        if (metadata?.GetMetadata<DenyReadOnlyAccessAttribute>() != null) return false;
        if (metadata?.GetMetadata<AllowReadOnlyAccessAttribute>() != null) return true;
        return HttpMethods.IsGet(http.Request.Method)
               || HttpMethods.IsHead(http.Request.Method)
               || HttpMethods.IsOptions(http.Request.Method);
    }
}
