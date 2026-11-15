using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MyCookbook.Api.Dto;

namespace MyCookbook.Services;

/// <summary>
/// Sends the planner shopping list to a Home Assistant webhook automation, which adds the items to a to-do list.
/// A webhook only triggers its own automation, so unlike a long-lived access token it exposes nothing else in HA.
/// Each user stores their own webhook URL in <see cref="PreferenceKey"/>.
/// </summary>
public partial class HomeAssistantService(IHttpClientFactory httpClientFactory, ILogger<HomeAssistantService> logger)
{
    public const string HttpClientName = "HomeAssistant";
    public const string PreferenceKey = "HomeAssistantWebhookUrl";
    public const int MaxItems = 500;

    [GeneratedRegex(@"^/api/webhook/[A-Za-z0-9_\-]+/?$")]
    private static partial Regex WebhookPathRegex();

    /// <summary>
    /// Only http(s) URLs of the form <c>{ha}/api/webhook/{id}</c> are accepted, so the server never posts to arbitrary URLs.
    /// </summary>
    public static bool IsValidWebhookUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && WebhookPathRegex().IsMatch(uri.AbsolutePath);

    /// <summary>The text of one to-do item, e.g. "Cibule: 3 ks".</summary>
    public static string Summary(ShoppingListItemDto item)
    {
        var name = item.Name.Trim();
        var amount = item.Amount?.Trim();
        return string.IsNullOrEmpty(amount) ? name : $"{name}: {amount}";
    }

    /// <returns>True if Home Assistant accepted the request.</returns>
    public async Task<bool> SendShoppingListAsync(string webhookUrl, IEnumerable<ShoppingListItemDto> items)
    {
        var payload = new
        {
            source = "MyCookbook",
            items = items.Select(i => new
            {
                summary = Summary(i),
                name = i.Name.Trim(),
                amount = i.Amount?.Trim() ?? "",
                recipes = i.Recipes,
            }).ToList(),
        };

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsJsonAsync(webhookUrl.Trim(), payload);
            if (response.IsSuccessStatusCode) return true;
            logger.LogWarning("Home Assistant webhook responded {Status}", (int)response.StatusCode);
            return false;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(e, "Home Assistant webhook could not be reached");
            return false;
        }
    }
}
