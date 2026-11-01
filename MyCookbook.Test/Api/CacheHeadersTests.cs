using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using MyCookbook.Api.Dto;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

// MC-72: a shared recipe link showed an outdated version of the recipe. API responses must never be
// reused by a browser or proxy, and index.html must be revalidated so new deploys are picked up.
public class CacheHeadersTests : IDisposable
{
    private readonly string _webRoot;
    private readonly WebRootFactory _factory;
    private readonly HttpClient _client;

    /// <summary>Serves static files from a temp folder, since the built SPA is not available in tests.</summary>
    private class WebRootFactory(string webRoot) : TestingWebAppFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseWebRoot(webRoot);
        }
    }

    public CacheHeadersTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "cache-headers-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>MyCookbook</title>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('app')");
        _factory = new WebRootFactory(_webRoot);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }

    private async Task<Guid> CreateRecipeAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/recipes", new { name, servings = 2 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RecipeDto>())!.Guid;
    }

    [Fact]
    public async Task SharedRecipe_IsNotStorable()
    {
        var guid = await CreateRecipeAsync("Gyros");

        var response = await _client.GetAsync($"/api/recipes/shared/{guid}");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task SharedRecipe_ReturnsLatestVersionAfterRename()
    {
        var guid = await CreateRecipeAsync("Gyros");
        await _client.GetAsync($"/api/recipes/shared/{guid}");

        var update = await _client.PutAsJsonAsync($"/api/recipes/{guid}", new { name = "Gyros v2", servings = 2 });
        update.EnsureSuccessStatusCode();
        var shared = await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/shared/{guid}");

        Assert.Equal("Gyros v2", shared!.Name);
    }

    [Theory]
    [InlineData("/api/recipes")]
    [InlineData("/api/changelog")]
    [InlineData("/api/recipes/shared/00000000-0000-0000-0000-000000000000")] // 404 must not be cached either
    public async Task ApiResponses_AreNotStorable(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.True(response.Headers.CacheControl?.NoStore, $"{url} returned {(int)response.StatusCode} without no-store");
    }

    [Theory]
    [InlineData("/recipe/shared/00000000-0000-0000-0000-000000000000")] // SPA fallback route
    [InlineData("/index.html")]
    public async Task IndexHtml_MustBeRevalidated(string url)
    {
        var response = await _client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        Assert.Contains("MyCookbook", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task HashedAssets_StayCacheable()
    {
        var response = await _client.GetAsync("/assets/index-abc123.js");

        response.EnsureSuccessStatusCode();
        Assert.Null(response.Headers.CacheControl);
    }
}
