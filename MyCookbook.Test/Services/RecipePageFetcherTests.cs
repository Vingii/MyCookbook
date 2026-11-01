using System.Net;
using MyCookbook.Services;

namespace MyCookbook.Test.Services;

public class RecipePageFetcherTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")] // cloud metadata
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:192.168.1.10")] // IPv4-mapped private address
    public void IsPublicAddress_RejectsInternalAddresses(string address)
    {
        Assert.False(RecipePageFetcher.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("172.32.0.1")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void IsPublicAddress_AllowsPublicAddresses(string address)
    {
        Assert.True(RecipePageFetcher.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Fact]
    public void ExtractRecipeJsonLd_FindsRecipeInGraph()
    {
        const string html = """
            <script type="application/ld+json">{"@type":"WebSite","name":"Site"}</script>
            <script type="application/ld+json">
              {"@context":"https://schema.org","@graph":[{"@type":"Organization"},{"@type":["Recipe","Thing"],"name":"Guláš"}]}
            </script>
            """;

        var json = RecipePageFetcher.ExtractRecipeJsonLd(html);

        Assert.NotNull(json);
        Assert.Contains("Guláš", json);
    }

    [Fact]
    public void ExtractRecipeJsonLd_IgnoresBrokenJsonAndPagesWithoutRecipe()
    {
        Assert.Null(RecipePageFetcher.ExtractRecipeJsonLd("""<script type="application/ld+json">{ broken</script>"""));
        Assert.Null(RecipePageFetcher.ExtractRecipeJsonLd("""<script type="application/ld+json">{"@type":"Article"}</script>"""));
    }

    [Fact]
    public void ExtractText_KeepsVisibleTextOnly()
    {
        const string html = """
            <html><head><title>T</title><style>.a{color:red}</style></head>
            <body><script>var tracking = 1;</script><h1>Guláš</h1><p>Cibuli nakrájejte &amp; osmahněte.</p></body></html>
            """;

        var text = RecipePageFetcher.ExtractText(html);

        Assert.Contains("Guláš", text);
        Assert.Contains("Cibuli nakrájejte & osmahněte.", text);
        Assert.DoesNotContain("tracking", text);
        Assert.DoesNotContain("color:red", text);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://localhost/")]
    [InlineData("http://[::1]/")]
    public async Task Handler_RefusesToConnectToInternalHosts(string url)
    {
        using var client = new HttpClient(RecipePageFetcher.CreateHandler());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(url));

        Assert.Contains("does not resolve to a public address", ex.ToString());
    }
}
