using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Packages;

public sealed record PackageSearchResult(
    string Id,
    string Version,
    string Description,
    string Authors,
    long TotalDownloads = 0,
    string IconUrl = "");

/// <summary>
/// Queries official package registries (NuGet, PyPI, crates.io, npm, pub.dev)
/// with non-blocking HTTP requests, strict timeouts, and offline resilience.
/// </summary>
public sealed class OnlinePackageSearchService
{
    public static OnlinePackageSearchService Instance { get; } = new();

    private readonly HttpClient _httpClient;

    public OnlinePackageSearchService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(6)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FrySharp", "1.0"));
        return client;
    }

    public async Task<IReadOnlyList<PackageSearchResult>> SearchAsync(string? languageId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<PackageSearchResult>();

        var normalizedLang = languageId?.ToLowerInvariant();
        try
        {
            return normalizedLang switch
            {
                "python" or "py" => await SearchPyPiAsync(query, ct),
                "rust" or "rs" => await SearchCratesIoAsync(query, ct),
                "javascript" or "js" or "node" => await SearchNpmAsync(query, ct),
                "dart" => await SearchPubDevAsync(query, ct),
                _ => await SearchNuGetAsync(query, ct)
            };
        }
        catch
        {
            return Array.Empty<PackageSearchResult>();
        }
    }

    public async Task<IReadOnlyList<PackageSearchResult>> SearchNuGetAsync(string query, CancellationToken ct = default)
    {
        var url = $"https://azuresearch-usnc.nuget.org/query?q={Uri.EscapeDataString(query)}&take=12&prerelease=false";
        var response = await _httpClient.GetFromJsonAsync<NuGetSearchResponse>(url, ct);
        if (response?.Data == null || response.Data.Count == 0) return Array.Empty<PackageSearchResult>();

        var list = new List<PackageSearchResult>(response.Data.Count);
        foreach (var item in response.Data)
        {
            var authors = item.Authors != null && item.Authors.Count > 0
                ? string.Join(", ", item.Authors)
                : (item.Id ?? string.Empty);

            list.Add(new PackageSearchResult(
                item.Id ?? "Unknown",
                item.Version ?? "1.0.0",
                item.Description ?? "No description provided.",
                authors,
                item.TotalDownloads,
                item.IconUrl ?? string.Empty));
        }
        return list;
    }

    public async Task<IReadOnlyList<PackageSearchResult>> SearchPyPiAsync(string query, CancellationToken ct = default)
    {
        var trimmed = query.Trim().ToLowerInvariant();
        var url = $"https://pypi.org/pypi/{Uri.EscapeDataString(trimmed)}/json";
        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<PackageSearchResult>();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("info", out var info))
            {
                var name = info.TryGetProperty("name", out var n) ? n.GetString() ?? trimmed : trimmed;
                var version = info.TryGetProperty("version", out var v) ? v.GetString() ?? "latest" : "latest";
                var summary = info.TryGetProperty("summary", out var s) ? s.GetString() ?? "Python package" : "Python package";
                var author = info.TryGetProperty("author", out var a) && !string.IsNullOrWhiteSpace(a.GetString())
                    ? a.GetString()!
                    : (info.TryGetProperty("author_email", out var ae) ? ae.GetString() ?? name : name);

                return new[]
                {
                    new PackageSearchResult(name, version, summary, author, 1_000_000)
                };
            }
        }
        catch
        {
            // Ignore network or parsing failure
        }
        return Array.Empty<PackageSearchResult>();
    }

    public async Task<IReadOnlyList<PackageSearchResult>> SearchCratesIoAsync(string query, CancellationToken ct = default)
    {
        var url = $"https://crates.io/api/v1/crates?q={Uri.EscapeDataString(query)}&per_page=12";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<PackageSearchResult>();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("crates", out var crates) || crates.ValueKind != JsonValueKind.Array)
            return Array.Empty<PackageSearchResult>();

        var list = new List<PackageSearchResult>();
        foreach (var crate in crates.EnumerateArray())
        {
            var name = crate.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(name)) continue;

            var version = crate.TryGetProperty("default_version", out var dv) && !string.IsNullOrWhiteSpace(dv.GetString())
                ? dv.GetString()!
                : (crate.TryGetProperty("max_version", out var mv) ? mv.GetString() ?? "1" : "1");

            var desc = crate.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var downloads = crate.TryGetProperty("downloads", out var dl) && dl.TryGetInt64(out var dls) ? dls : 0;

            list.Add(new PackageSearchResult(name, version, desc, "crates.io", downloads));
        }
        return list;
    }

    public async Task<IReadOnlyList<PackageSearchResult>> SearchNpmAsync(string query, CancellationToken ct = default)
    {
        var url = $"https://registry.npmjs.org/-/v1/search?text={Uri.EscapeDataString(query)}&size=12";
        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<PackageSearchResult>();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array)
            return Array.Empty<PackageSearchResult>();

        var list = new List<PackageSearchResult>();
        foreach (var obj in objects.EnumerateArray())
        {
            if (!obj.TryGetProperty("package", out var pkg)) continue;
            var name = pkg.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(name)) continue;

            var version = pkg.TryGetProperty("version", out var v) ? v.GetString() ?? "1.0.0" : "1.0.0";
            var desc = pkg.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var publisher = pkg.TryGetProperty("publisher", out var pub) && pub.TryGetProperty("username", out var u)
                ? u.GetString() ?? "npm"
                : "npm";

            list.Add(new PackageSearchResult(name, version, desc, publisher, 100_000));
        }
        return list;
    }

    public async Task<IReadOnlyList<PackageSearchResult>> SearchPubDevAsync(string query, CancellationToken ct = default)
    {
        var url = $"https://pub.dev/api/search?q={Uri.EscapeDataString(query)}";
        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<PackageSearchResult>();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("packages", out var pkgs) || pkgs.ValueKind != JsonValueKind.Array)
            return Array.Empty<PackageSearchResult>();

        var list = new List<PackageSearchResult>();
        foreach (var item in pkgs.EnumerateArray())
        {
            if (item.TryGetProperty("package", out var p) && p.GetString() is { } name && !string.IsNullOrWhiteSpace(name))
            {
                list.Add(new PackageSearchResult(name, "latest", $"Dart package '{name}'", "pub.dev", 50_000));
            }
        }
        return list;
    }

    private sealed class NuGetSearchResponse
    {
        [JsonPropertyName("totalHits")]
        public int TotalHits { get; set; }

        [JsonPropertyName("data")]
        public List<NuGetSearchData>? Data { get; set; }
    }

    private sealed class NuGetSearchData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("authors")]
        public List<string>? Authors { get; set; }

        [JsonPropertyName("totalDownloads")]
        public long TotalDownloads { get; set; }

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; set; }
    }
}
