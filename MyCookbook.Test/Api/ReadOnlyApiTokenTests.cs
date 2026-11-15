using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Services;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// MC-78: API tokens (used by Home Assistant) give read-only access.
public class ReadOnlyApiTokenTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly Guid _guid = Guid.NewGuid();
    private string _token = "";

    private record MeResponse(string Username, bool IsReadOnly);

    public ReadOnlyApiTokenTests()
    {
        _factory = new TestingWebAppFactory<Program>();
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>Seeds a "Gyros" recipe for "anna", planned on 2026-11-16, and gives anna an API token.</summary>
    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using (var ctx = await dbFactory.CreateDbContextAsync())
        {
            var recipe = new Recipe { Guid = _guid, Name = "Gyros", UserName = "anna" };
            recipe.Ingredients.Add(new Ingredient { Name = "Pork", Amount = "500 g", Order = 1, UserName = "anna" });
            ctx.Recipes.Add(recipe);
            ctx.PlannedRecipes.Add(new PlannedRecipe { Id = 1, UserName = "anna", Recipe = recipe, Date = new DateOnly(2026, 11, 16) });
            await ctx.SaveChangesAsync();
        }
        _token = await scope.ServiceProvider.GetRequiredService<ApiTokenService>().GenerateTokenAsync("anna");
    }

    private HttpRequestMessage WithToken(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<HttpStatusCode> SendAsync(HttpMethod method, string url, object? body = null) =>
        (await _client.SendAsync(WithToken(method, url, body))).StatusCode;

    [Fact]
    public async Task Token_CanReadRecipesAndPlans()
    {
        await SeedAsync();

        var recipes = await (await _client.SendAsync(WithToken(HttpMethod.Get, "/api/recipes"))).Content.ReadFromJsonAsync<List<RecipeDto>>();
        Assert.Equal(["Gyros"], recipes!.Select(r => r.Name));

        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, $"/api/recipes/{_guid}"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/planner?from=2026-11-16&to=2026-11-22"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/planner/ingredients?from=2026-11-16&to=2026-11-22"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/tags"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/export"));
    }

    [Fact]
    public async Task Me_ReportsReadOnly()
    {
        await SeedAsync();

        var me = await (await _client.SendAsync(WithToken(HttpMethod.Get, "/api/auth/me"))).Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal("anna", me!.Username);
        Assert.True(me.IsReadOnly);
    }

    [Fact]
    public async Task Me_WithoutToken_IsNotReadOnly()
    {
        var me = await _client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.False(me!.IsReadOnly);
    }

    public static TheoryData<string, string> Writes => new()
    {
        { "POST", "/api/recipes" },
        { "PUT", "/api/recipes/{guid}" },
        { "DELETE", "/api/recipes/{guid}" },
        { "POST", "/api/recipes/{guid}/clone" },
        { "POST", "/api/recipes/{guid}/lastcooked" },
        { "POST", "/api/recipes/{guid}/ingredients" },
        { "POST", "/api/recipes/{guid}/steps" },
        { "POST", "/api/recipes/{guid}/tags" },
        { "POST", "/api/planner" },
        { "PUT", "/api/planner/1" },
        { "DELETE", "/api/planner/1" },
        { "POST", "/api/import" },
        { "POST", "/api/shared/recipes/{guid}" },
        { "PUT", "/api/homeassistant" },
        { "DELETE", "/api/auth/token" },
        { "POST", "/api/auth/share-token" },
        { "DELETE", "/api/auth/share-token" },
    };

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task Token_CannotChangeAnything(string method, string url)
    {
        await SeedAsync();

        var request = WithToken(new HttpMethod(method), url.Replace("{guid}", _guid.ToString()), new { });
        // Import only accepts a file upload; send one so the request reaches authorization.
        if (url == "/api/import")
            request.Content = new MultipartFormDataContent { { new StringContent("[]"), "file", "export.json" } };

        var status = (await _client.SendAsync(request)).StatusCode;

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task Token_WritesLeaveDataUntouched()
    {
        await SeedAsync();

        await SendAsync(HttpMethod.Delete, $"/api/recipes/{_guid}");
        await SendAsync(HttpMethod.Delete, "/api/planner/1");

        var recipe = await _client.SendAsync(WithToken(HttpMethod.Get, $"/api/recipes/{_guid}"));
        Assert.Equal(HttpStatusCode.OK, recipe.StatusCode);
        var plans = await (await _client.SendAsync(WithToken(HttpMethod.Get, "/api/planner?from=2026-11-16&to=2026-11-16")))
            .Content.ReadFromJsonAsync<List<PlannedRecipeDto>>();
        Assert.Single(plans!);
    }

    [Theory]
    [InlineData("/api/auth/token")]
    [InlineData("/api/auth/share-token")]
    [InlineData("/api/homeassistant")]
    public async Task Token_CannotReadAccountSecrets(string url)
    {
        await SeedAsync();
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Get, url));
    }

    [Fact]
    public async Task Token_CanMarkChangelogAsSeen()
    {
        await SeedAsync();
        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Put, "/api/changelog/lastSeen"));
    }

    [Fact]
    public async Task WithoutToken_WritesStillWork()
    {
        var response = await _client.PostAsJsonAsync("/api/recipes", new { name = "Tzatziki" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TokenLink_SignsInReadOnlySession()
    {
        await SeedAsync();
        var auth = new CapturingAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Path = "/planner";
        http.Request.QueryString = new QueryString($"?token={_token}&x=1");

        using var scope = _factory.Services.CreateScope();
        var middleware = new TokenQueryParamMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(http, scope.ServiceProvider.GetRequiredService<ApiTokenService>());

        Assert.Equal("anna", auth.SignedIn!.Identity!.Name);
        Assert.True(auth.SignedIn.IsReadOnly());
        Assert.Equal("/planner?x=1", http.Response.Headers.Location.ToString());
    }

    private sealed class CapturingAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? SignedIn { get; private set; }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignedIn = principal;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
    }
}
