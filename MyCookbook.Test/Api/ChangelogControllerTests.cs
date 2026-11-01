using System.Net.Http.Json;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Api;

public class ChangelogControllerTests : IDisposable
{
    private readonly TestingWebAppFactory<Program> _factory;
    private readonly HttpClient _client;

    private record EntryResponse(string Version, DateTime? ReleaseDate, string RawHtml);

    public ChangelogControllerTests()
    {
        _factory = new TestingWebAppFactory<Program>();
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetEntries_WithoutLang_ReturnsEnglish()
    {
        var entries = await _client.GetFromJsonAsync<List<EntryResponse>>("/api/changelog");

        Assert.NotNull(entries);
        Assert.Contains(entries, e => e.RawHtml.Contains("Patch notes are now available in Czech."));
    }

    [Fact]
    public async Task GetEntries_WithCzechLang_ReturnsCzech()
    {
        var entries = await _client.GetFromJsonAsync<List<EntryResponse>>("/api/changelog?lang=cs");

        Assert.NotNull(entries);
        Assert.Contains(entries, e => e.RawHtml.Contains("Seznam změn je nyní dostupný i v češtině."));
        Assert.DoesNotContain(entries, e => e.RawHtml.Contains("Patch notes are now available in Czech."));
    }
}
