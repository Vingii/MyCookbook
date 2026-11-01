using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyCookbook.Services;

/// <summary>
/// Downloads a recipe web page a user pasted and reduces it to what the LLM needs (MC-69).
/// Users control the URL, so every connection, including after redirects, must go to a public address:
/// the app runs next to internal services (LiteLLM, the database) that must not be reachable this way.
/// </summary>
public class RecipePageFetcher(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "RecipePage";
    private const int MaxPageBytes = 3 * 1024 * 1024;
    private const int MaxTextChars = 40_000;

    public class FetchException(string code, string message) : Exception(message)
    {
        /// <summary>Stable code the UI translates: invalid_url, unreachable, not_html, too_large.</summary>
        public string Code { get; } = code;
    }

    /// <summary>The page reduced for the LLM: its schema.org Recipe data when present, otherwise visible text.</summary>
    public record PageContent(string Url, string Title, string? RecipeJsonLd, string Text);

    public async Task<PageContent> FetchAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !uri.IsDefaultPort)
            throw new FetchException("invalid_url", $"Not an http(s) URL on a standard port: {url}");

        var client = httpClientFactory.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FetchException("unreachable", $"Could not download {uri}: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new FetchException("unreachable", $"{uri} returned {(int)response.StatusCode}");

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && mediaType != "text/html" && mediaType != "application/xhtml+xml")
                throw new FetchException("not_html", $"{uri} is {mediaType}, not a web page");

            var html = await ReadLimitedAsync(response.Content, ct);
            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? uri.ToString();
            return new PageContent(finalUrl, ExtractTitle(html), ExtractRecipeJsonLd(html), ExtractText(html));
        }
    }

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxPageBytes)
                throw new FetchException("too_large", "The page is larger than 3 MB");
            buffer.Write(chunk, 0, read);
        }
        var charset = content.Headers.ContentType?.CharSet?.Trim('"');
        var encoding = Encoding.UTF8;
        try { if (!string.IsNullOrEmpty(charset)) encoding = Encoding.GetEncoding(charset); }
        catch (ArgumentException) { }
        return encoding.GetString(buffer.ToArray());
    }

    private static string ExtractTitle(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : "";
    }

    /// <summary>
    /// Most recipe sites embed a schema.org Recipe as JSON-LD. It is far more reliable than page text,
    /// so it is handed to the LLM when present.
    /// </summary>
    public static string? ExtractRecipeJsonLd(string html)
    {
        var scripts = Regex.Matches(html,
            @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match script in scripts)
        {
            try
            {
                using var doc = JsonDocument.Parse(script.Groups[1].Value.Trim());
                if (FindRecipe(doc.RootElement) is { } recipe)
                    return recipe.GetRawText();
            }
            catch (JsonException)
            {
                // Broken JSON-LD is common; fall back to page text.
            }
        }
        return null;
    }

    private static JsonElement? FindRecipe(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (FindRecipe(item) is { } found) return found;
                break;
            case JsonValueKind.Object:
                if (element.TryGetProperty("@type", out var type) && IsRecipeType(type)) return element;
                if (element.TryGetProperty("@graph", out var graph)) return FindRecipe(graph);
                break;
        }
        return null;
    }

    private static bool IsRecipeType(JsonElement type) => type.ValueKind switch
    {
        JsonValueKind.String => type.GetString() == "Recipe",
        JsonValueKind.Array => type.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && t.GetString() == "Recipe"),
        _ => false,
    };

    /// <summary>Visible text of the page, without scripts, styles and markup, capped for the LLM.</summary>
    public static string ExtractText(string html)
    {
        var text = Regex.Replace(html, @"<(script|style|noscript|svg|head)\b.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<(br|/p|/div|/li|/h[1-6]|/tr)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t\r\f\v]+", " ");
        text = Regex.Replace(text, @"\s*\n\s*", "\n").Trim();
        return text.Length > MaxTextChars ? text[..MaxTextChars] : text;
    }

    /// <summary>
    /// Handler for <see cref="HttpClientName"/>: resolves every connection itself and refuses non-public
    /// addresses, which also covers redirects and DNS names pointing at internal hosts.
    /// </summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var allowed = addresses.FirstOrDefault(IsPublicAddress)
                ?? throw new HttpRequestException($"{context.DnsEndPoint.Host} does not resolve to a public address");
            var socket = new Socket(allowed.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(allowed, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0                                   // "this" network
                  || b[0] == 10                                  // private
                  || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)  // carrier-grade NAT
                  || (b[0] == 169 && b[1] == 254)                // link-local, cloud metadata
                  || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // private
                  || (b[0] == 192 && b[1] == 168)                // private
                  || (b[0] == 192 && b[1] == 0 && b[2] == 0)     // IETF protocol assignments
                  || (b[0] == 198 && (b[1] == 18 || b[1] == 19)) // benchmarking
                  || b[0] >= 224);                               // multicast, reserved, broadcast
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.Equals(IPAddress.IPv6None)
                  || address.IsIPv6LinkLocal
                  || address.IsIPv6SiteLocal
                  || address.IsIPv6Multicast
                  || (b[0] & 0xFE) == 0xFC);                     // unique local fc00::/7
        }

        return false;
    }
}
