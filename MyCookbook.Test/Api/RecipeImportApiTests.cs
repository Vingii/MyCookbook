using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Model;
using MyCookbook.Services;
using MyCookbook.Test.Common;
using RichardSzalay.MockHttp;

namespace MyCookbook.Test.Api;

// MC-69: importing a recipe from a web page link with AI. The web page and LiteLLM are faked.
// Authenticates as "devuser" — see the note on RecipesControllerTests.
public class RecipeImportApiTests : IDisposable
{
    private const string LiteLlmUrl = "http://litellm.test";
    private const string PageUrl = "https://recipes.example/gulas";

    private readonly MockHttpMessageHandler _web = new();
    private readonly MockHttpMessageHandler _llm = new();
    private readonly ImportFactory _factory;
    private readonly HttpClient _client;
    private string? _llmRequest;

    private class ImportFactory(MockHttpMessageHandler web, MockHttpMessageHandler llm, bool configured) : TestingWebAppFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            if (configured)
            {
                builder.UseSetting("LiteLLM:BaseUrl", LiteLlmUrl);
                builder.UseSetting("LiteLLM:Model", "test-model");
                builder.UseSetting("LiteLLM:ApiKey", "test-key");
            }
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(RecipePageFetcher.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => web);
                services.AddHttpClient(LlmClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => llm);
            });
        }
    }

    public RecipeImportApiTests() : this(configured: true) { }

    private RecipeImportApiTests(bool configured)
    {
        _factory = new ImportFactory(_web, _llm, configured);
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private const string PageHtml = """
        <html><head><title>Guláš | Recepty</title>
        <script type="application/ld+json">{"@type":"Recipe","name":"Hovězí guláš","recipeIngredient":["1 kg hovězí kližky","3 cibule"]}</script>
        </head><body><p>Nejlepší guláš.</p></body></html>
        """;

    private static readonly object SuggestedRecipe = new
    {
        name = "Hovězí guláš",
        category = "Hlavní jídla",
        servings = "4 porce", // models mix up types
        duration = 150,
        tags = new[] { "tradiční", "tradiční", "" },
        ingredients = new object[]
        {
            new { name = "hovězí kližka", amount = "1 kg" },
            new { name = "cibule", amount = "3 ks" },
            new { name = "  ", amount = "1 ks" }, // dropped: no name
        },
        steps = new object[]
        {
            new { description = "Cibuli nakrájejte a osmahněte.", durationMinutes = 15, stepType = "Active" },
            new { description = "Duste pod pokličkou.", durationMinutes = 120, stepType = "Passive" },
            new { description = "Dochuťte.", durationMinutes = (int?)null, stepType = "7" }, // invalid type → Active
        },
    };

    private static readonly object OriginalRecipe = new
    {
        name = "Guláš",
        servings = 4,
        ingredients = new object[] { new { name = "hovězí kližky", amount = "1 kg" } },
        steps = new object[] { new { description = "Uvařte guláš.", stepType = "Active" } },
    };

    private void ServePage(string html = PageHtml, string mediaType = "text/html") =>
        _web.When(PageUrl).Respond(mediaType, html);

    /// <summary>LiteLLM replies with <paramref name="content"/> as the assistant message, and the request is captured.</summary>
    private void LlmReplies(string content) =>
        _llm.When(HttpMethod.Post, $"{LiteLlmUrl}/v1/chat/completions").Respond(async request =>
        {
            _llmRequest = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { choices = new[] { new { message = new { role = "assistant", content } } } }),
            };
        });

    private void LlmRepliesWithRecipes() =>
        LlmReplies("```json\n" + JsonSerializer.Serialize(new { suggested = SuggestedRecipe, original = OriginalRecipe }) + "\n```");

    private Task<HttpResponseMessage> PreviewAsync(string url = PageUrl, string language = "cs") =>
        _client.PostAsJsonAsync("/api/recipe-import/preview", new { url, language });

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    private async Task SeedConventionsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var context = await dbFactory.CreateDbContextAsync();
        var recipe = new Recipe { Name = "Česnečka", Category = "Polévky", UserName = "devuser", Servings = 4 };
        recipe.Ingredients.Add(new Ingredient { Name = "česnek", Amount = "6 stroužků", Order = 1, UserName = "devuser" });
        recipe.Steps.Add(new Step { Description = "Brambory nakrájejte na kostičky a uvařte.", Order = 1, UserName = "devuser" });
        recipe.Tags = [new Tag { Name = "rychlé", UserName = "devuser" }];
        context.Recipes.Add(recipe);
        context.Recipes.Add(new Recipe { Name = "Hovězí guláš", UserName = "devuser", Servings = 4 });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Status_IsEnabledWhenLiteLlmIsConfigured()
    {
        var status = await _client.GetFromJsonAsync<RecipeImportStatusDto>("/api/recipe-import/status");

        Assert.True(status!.Enabled);
    }

    [Fact]
    public async Task Status_IsDisabledAndPreviewUnavailable_WithoutConfiguration()
    {
        using var unconfigured = new ImportFactory(new MockHttpMessageHandler(), new MockHttpMessageHandler(), configured: false);
        var client = unconfigured.CreateClient();

        var status = await client.GetFromJsonAsync<RecipeImportStatusDto>("/api/recipe-import/status");
        var preview = await client.PostAsJsonAsync("/api/recipe-import/preview", new { url = PageUrl, language = "cs" });

        Assert.False(status!.Enabled);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, preview.StatusCode);
        Assert.Equal("not_configured", await ErrorCodeAsync(preview));
    }

    [Fact]
    public async Task Preview_ReturnsSuggestedThenOriginal_Sanitized()
    {
        ServePage();
        LlmRepliesWithRecipes();

        var response = await PreviewAsync();

        response.EnsureSuccessStatusCode();
        var preview = (await response.Content.ReadFromJsonAsync<RecipeImportPreviewDto>())!;
        Assert.Equal(PageUrl, preview.SourceUrl);
        Assert.Equal(["suggested", "original"], preview.Variants.Select(v => v.Key));

        var suggested = preview.Variants[0].Recipe;
        Assert.Equal("Hovězí guláš", suggested.Name);
        Assert.Equal(4, suggested.Servings);
        Assert.Equal(150, suggested.Duration);
        Assert.Equal(["tradiční"], suggested.Tags);
        Assert.Equal(["hovězí kližka", "cibule"], suggested.Ingredients.Select(i => i.Name));
        Assert.Equal(["Active", "Passive", "Active"], suggested.Steps.Select(s => s.StepType));
        Assert.Equal("Uvařte guláš.", Assert.Single(preview.Variants[1].Recipe.Steps).Description);
    }

    [Fact]
    public async Task Preview_SendsPageDataUserConventionsAndTargetLanguageToTheModel()
    {
        await SeedConventionsAsync();
        ServePage();
        LlmRepliesWithRecipes();

        await PreviewAsync(language: "en");

        using var request = JsonDocument.Parse(_llmRequest!);
        Assert.Equal("test-model", request.RootElement.GetProperty("model").GetString());
        var messages = request.RootElement.GetProperty("messages");
        var system = messages[0].GetProperty("content").GetString()!;
        var user = messages[1].GetProperty("content").GetString()!;
        Assert.Contains("Written in English", system);
        Assert.Contains("hovězí kližky", user);           // JSON-LD from the page
        Assert.Contains("Polévky", user);                 // the user's category
        Assert.Contains("rychlé", user);                  // tag
        Assert.Contains("česnek", user);                  // ingredient name
        Assert.Contains("6 stroužků", user);              // amount format
        Assert.Contains("Brambory nakrájejte", user);     // step style
    }

    [Fact]
    public async Task Preview_DoesNotLeakOtherUsersConventions()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
            await using var context = await dbFactory.CreateDbContextAsync();
            context.Recipes.Add(new Recipe { Name = "Secret", Category = "Tajná kategorie", UserName = "someone-else", Servings = 1 });
            await context.SaveChangesAsync();
        }
        ServePage();
        LlmRepliesWithRecipes();

        await PreviewAsync();

        Assert.DoesNotContain("Tajná kategorie", _llmRequest);
    }

    [Theory]
    [InlineData("ftp://recipes.example/x")]
    [InlineData("https://recipes.example:8443/x")]
    [InlineData("not a url")]
    public async Task Preview_RejectsUnsupportedUrls(string url)
    {
        var response = await PreviewAsync(url);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_url", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Preview_ReportsUnreachablePage()
    {
        _web.When(PageUrl).Respond(HttpStatusCode.NotFound);

        Assert.Equal("unreachable", await ErrorCodeAsync(await PreviewAsync()));
    }

    [Fact]
    public async Task Preview_ReportsNonHtmlLinks()
    {
        ServePage("%PDF-1.7", "application/pdf");

        Assert.Equal("not_html", await ErrorCodeAsync(await PreviewAsync()));
    }

    [Fact]
    public async Task Preview_ReportsPagesWithoutRecipe()
    {
        ServePage("<html><body>Hello</body></html>");
        LlmReplies("""{"error": "no_recipe"}""");

        Assert.Equal("no_recipe", await ErrorCodeAsync(await PreviewAsync()));
    }

    [Fact]
    public async Task Preview_ReportsModelFailures()
    {
        ServePage();
        _llm.When(HttpMethod.Post, $"{LiteLlmUrl}/v1/chat/completions").Respond(HttpStatusCode.InternalServerError);

        Assert.Equal("ai_failed", await ErrorCodeAsync(await PreviewAsync()));
    }

    [Fact]
    public async Task Preview_ReportsUnparseableReplies()
    {
        ServePage();
        LlmReplies("Sorry, I cannot help with that.");

        Assert.Equal("ai_failed", await ErrorCodeAsync(await PreviewAsync()));
    }

    [Fact]
    public async Task Save_CreatesCompleteRecipeOwnedByUser_WithUniqueName()
    {
        await SeedConventionsAsync(); // already has "Hovězí guláš"
        var draft = new RecipeDraftDto
        {
            Name = "Hovězí guláš",
            Category = "Hlavní jídla",
            Servings = 4,
            Duration = 150,
            Tags = ["tradiční"],
            Ingredients = [new() { Name = "hovězí kližka", Amount = "1 kg" }, new() { Name = "cibule", Amount = "3 ks" }],
            Steps =
            [
                new() { Description = "Cibuli osmahněte.", DurationMinutes = 15, StepType = "Active" },
                new() { Description = "Duste.", DurationMinutes = 120, StepType = "Passive" },
            ],
        };

        var response = await _client.PostAsJsonAsync("/api/recipe-import/save", draft);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<RecipeDto>())!;
        Assert.Equal("Hovězí guláš (2)", created.Name);
        var fetched = (await _client.GetFromJsonAsync<RecipeDto>($"/api/recipes/{created.Guid}"))!;
        Assert.Equal(["hovězí kližka", "cibule"], fetched.Ingredients.Select(i => i.Name));
        Assert.Equal(["Cibuli osmahněte.", "Duste."], fetched.Steps.Select(s => s.Description));
        Assert.Equal(["tradiční"], fetched.Tags);

        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CookbookDatabaseContext>>();
        await using var context = await dbFactory.CreateDbContextAsync();
        var saved = await context.Recipes.Include(r => r.Ingredients).Include(r => r.Steps).Include(r => r.Tags)
            .SingleAsync(r => r.Guid == created.Guid);
        Assert.All(saved.Ingredients, i => Assert.Equal("devuser", i.UserName));
        Assert.All(saved.Steps, s => Assert.Equal("devuser", s.UserName));
        Assert.All(saved.Tags, t => Assert.Equal("devuser", t.UserName));
        Assert.Equal(StepType.Passive, saved.Steps.Single(s => s.Order == 2).StepType);
        Assert.Equal(TimeSpan.FromMinutes(120), saved.Steps.Single(s => s.Order == 2).Duration);
    }

    [Fact]
    public async Task Save_EnforcesLimitsOnClientData()
    {
        var draft = new RecipeDraftDto
        {
            Name = new string('x', 80),
            Servings = 0,
            Ingredients = [new() { Name = new string('y', 80), Amount = "1" }],
            Steps = [new() { Description = "Mix.", StepType = "Bogus" }],
        };

        var created = (await (await _client.PostAsJsonAsync("/api/recipe-import/save", draft)).Content.ReadFromJsonAsync<RecipeDto>())!;

        Assert.Equal(50, created.Name.Length);
        Assert.Equal(1, created.Servings);
        Assert.Equal(50, Assert.Single(created.Ingredients).Name.Length);
        Assert.Equal("Active", Assert.Single(created.Steps).StepType);
    }

    [Theory]
    [InlineData("Gyros", new string[0], "Gyros")]
    [InlineData("Gyros", new[] { "Gyros" }, "Gyros (2)")]
    [InlineData("Gyros", new[] { "Gyros", "Gyros (2)" }, "Gyros (3)")]
    public void UniqueName_AddsNumberWhenTaken(string name, string[] existing, string expected)
    {
        Assert.Equal(expected, RecipeImportService.UniqueName(name, existing));
    }

    [Fact]
    public void UniqueName_StaysWithinColumnLimit()
    {
        var name = new string('a', 50);

        var unique = RecipeImportService.UniqueName(name, [name]);

        Assert.Equal(50, unique.Length);
        Assert.EndsWith(" (2)", unique);
    }
}
