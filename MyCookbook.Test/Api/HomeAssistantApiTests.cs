using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MyCookbook.Api.Dto;
using MyCookbook.Services;
using MyCookbook.Test.Common;
using RichardSzalay.MockHttp;

namespace MyCookbook.Test.Api;

// MC-77: sending the planner shopping list to a Home Assistant webhook. HA is faked.
// Authenticates as "devuser" — see the note on RecipesControllerTests.
public class HomeAssistantApiTests : IDisposable
{
    private const string WebhookUrl = "http://ha.local:8123/api/webhook/mycookbook-abc123";
    private readonly MockHttpMessageHandler _ha = new();
    private readonly HaFactory _factory;
    private readonly HttpClient _client;

    private class HaFactory(MockHttpMessageHandler ha) : TestingWebAppFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
                services.AddHttpClient(HomeAssistantService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => ha));
        }
    }

    public HomeAssistantApiTests()
    {
        _factory = new HaFactory(_ha);
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private static SendShoppingListRequest Request() => new()
    {
        Items =
        [
            new ShoppingListItemDto { Name = "Cibule", Amount = "3 ks", Recipes = ["Gyros", "Guláš"] },
            new ShoppingListItemDto { Name = " Sůl ", Amount = "", Recipes = ["Guláš"] },
        ],
    };

    private async Task ConfigureAsync(string url = WebhookUrl)
    {
        var response = await _client.PutAsJsonAsync("/api/homeassistant", new HomeAssistantSettingsDto { WebhookUrl = url });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Get_NotConfigured_ReturnsNull()
    {
        var settings = await _client.GetFromJsonAsync<HomeAssistantSettingsDto>("/api/homeassistant");
        Assert.Null(settings!.WebhookUrl);
    }

    [Fact]
    public async Task Put_ThenGet_ReturnsUrl_AndDeleteClearsIt()
    {
        await ConfigureAsync($"  {WebhookUrl} ");
        Assert.Equal(WebhookUrl, (await _client.GetFromJsonAsync<HomeAssistantSettingsDto>("/api/homeassistant"))!.WebhookUrl);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/homeassistant")).StatusCode);
        Assert.Null((await _client.GetFromJsonAsync<HomeAssistantSettingsDto>("/api/homeassistant"))!.WebhookUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://ha.local/api/webhook/x")]
    [InlineData("http://ha.local:8123/api/services/todo/add_item")]
    [InlineData("http://ha.local:8123/api/webhook/x/../../services")]
    [InlineData("http://user:pw@ha.local/api/webhook/x")]
    [InlineData("http://ha.local/api/webhook/x?y=1")]
    public async Task Put_InvalidUrl_ReturnsBadRequest(string url)
    {
        var response = await _client.PutAsJsonAsync("/api/homeassistant", new HomeAssistantSettingsDto { WebhookUrl = url });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("http://192.168.1.5:8123/api/webhook/mycookbook-abc123")]
    [InlineData("https://ha.example.com/api/webhook/Some_ID-1/")]
    public void IsValidWebhookUrl_AcceptsWebhookUrls(string url)
    {
        Assert.True(HomeAssistantService.IsValidWebhookUrl(url));
    }

    [Fact]
    public async Task Send_PostsItemsToWebhook()
    {
        await ConfigureAsync();
        string? body = null;
        _ha.Expect(HttpMethod.Post, WebhookUrl).With(r => { body = r.Content!.ReadAsStringAsync().Result; return true; })
            .Respond(HttpStatusCode.OK);

        var response = await _client.PostAsJsonAsync("/api/homeassistant/shopping-list", Request());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _ha.VerifyNoOutstandingExpectation();
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("MyCookbook", json.RootElement.GetProperty("source").GetString());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Cibule: 3 ks", "Sůl"], items.Select(i => i.GetProperty("summary").GetString()));
        Assert.Equal("3 ks", items[0].GetProperty("amount").GetString());
        Assert.Equal(["Gyros", "Guláš"], items[0].GetProperty("recipes").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Send_NotConfigured_ReturnsConflict()
    {
        var response = await _client.PostAsJsonAsync("/api/homeassistant/shopping-list", Request());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Send_NoItems_ReturnsBadRequest()
    {
        await ConfigureAsync();
        var response = await _client.PostAsJsonAsync("/api/homeassistant/shopping-list",
            new SendShoppingListRequest { Items = [new ShoppingListItemDto { Name = " " }] });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Send_HomeAssistantError_ReturnsBadGateway()
    {
        await ConfigureAsync();
        _ha.When(HttpMethod.Post, WebhookUrl).Respond(HttpStatusCode.InternalServerError);

        var response = await _client.PostAsJsonAsync("/api/homeassistant/shopping-list", Request());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Send_HomeAssistantUnreachable_ReturnsBadGateway()
    {
        await ConfigureAsync();
        _ha.When(HttpMethod.Post, WebhookUrl).Throw(new HttpRequestException("connection refused"));

        var response = await _client.PostAsJsonAsync("/api/homeassistant/shopping-list", Request());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
