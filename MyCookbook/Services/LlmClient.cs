using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyCookbook.Services;

/// <summary>
/// Calls the LiteLLM proxy through its OpenAI-compatible chat completions API (MC-69).
/// Configured with LiteLLM__BaseUrl, LiteLLM__ApiKey and LiteLLM__Model; disabled when the URL or model is missing.
/// </summary>
public class LlmClient(IHttpClientFactory httpClientFactory, IConfiguration config)
{
    public const string HttpClientName = "LiteLLM";

    public class LlmException(string message) : Exception(message);

    private string? BaseUrl => config["LiteLLM:BaseUrl"];
    private string? Model => config["LiteLLM:Model"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);

    private record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    /// <summary>Sends one system + user message and returns the assistant's text.</summary>
    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new LlmException("LiteLLM is not configured");

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl!.TrimEnd('/')}/v1/chat/completions")
        {
            Content = JsonContent.Create(new ChatRequest(
                Model!,
                [new ChatMessage("system", system), new ChatMessage("user", user)],
                Temperature: 0.2,
                MaxTokens: 8000)),
        };
        if (config["LiteLLM:ApiKey"] is { Length: > 0 } apiKey)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LlmException($"LiteLLM request failed: {ex.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new LlmException($"LiteLLM returned {(int)response.StatusCode}: {body[..Math.Min(body.Length, 500)]}");

            try
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                    ?? throw new LlmException("LiteLLM returned an empty message");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
            {
                throw new LlmException($"Unexpected LiteLLM response: {ex.Message}");
            }
        }
    }
}
