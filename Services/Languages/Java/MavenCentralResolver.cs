using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

public sealed record MavenArtifactCoordinate(string GroupId, string ArtifactId, string Version)
{
    public string GroupPath => GroupId.Replace('.', '/');
    public string FileName => $"{ArtifactId}-{Version}.jar";
    public string PomFileName => $"{ArtifactId}-{Version}.pom";
    public string Canonical => $"{GroupId}:{ArtifactId}:{Version}";

    public static bool TryParse(string text, out MavenArtifactCoordinate coordinate)
    {
        coordinate = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Trim().Split(':');
        if (parts.Length < 2) return false;

        var group = parts[0].Trim();
        var artifact = parts[1].Trim();
        var version = parts.Length >= 3 ? parts[2].Trim() : "LATEST";

        if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(artifact)) return false;

        coordinate = new MavenArtifactCoordinate(group, artifact, version);
        return true;
    }
}

public sealed record MavenSearchResultItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("g")] string GroupId,
    [property: JsonPropertyName("a")] string ArtifactId,
    [property: JsonPropertyName("latestVersion")] string LatestVersion,
    [property: JsonPropertyName("versionCount")] int VersionCount);

public sealed class MavenCentralResolver
{
    private const string CentralRepoBase = "https://repo1.maven.org/maven2";
    private const string SearchApiBase = "https://search.maven.org/solrsearch/select";

    private readonly HttpClient _http;
    private readonly IHostEnvironment _host;
    private readonly string _cacheDirectory;

    public MavenCentralResolver(IHostEnvironment host, HttpClient? httpClient = null, string? customCacheDir = null)
    {
        _host = host;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var m2Repo = Path.Combine(userHome, ".m2", "repository");
        _cacheDirectory = customCacheDir ?? m2Repo;
    }

    public string GetLocalJarPath(MavenArtifactCoordinate coord)
    {
        return Path.Combine(_cacheDirectory, coord.GroupPath, coord.ArtifactId, coord.Version, coord.FileName);
    }

    public bool IsCached(MavenArtifactCoordinate coord)
    {
        return _host.FileExists(GetLocalJarPath(coord));
    }

    public async Task<string> ResolveLatestVersionAsync(string groupId, string artifactId, CancellationToken ct = default)
    {
        var groupPath = groupId.Replace('.', '/');
        var metadataUrl = $"{CentralRepoBase}/{groupPath}/{artifactId}/maven-metadata.xml";

        try
        {
            using var response = await _http.GetAsync(metadataUrl, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var xmlText = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var doc = XDocument.Parse(xmlText);
                var release = doc.Root?.Element("versioning")?.Element("release")?.Value
                              ?? doc.Root?.Element("versioning")?.Element("latest")?.Value;
                if (!string.IsNullOrWhiteSpace(release)) return release;

                var versions = doc.Root?.Element("versioning")?.Element("versions")?.Elements("version")
                    .Select(x => x.Value).ToList();
                if (versions != null && versions.Count > 0)
                {
                    return versions.Last();
                }
            }
        }
        catch
        {
            // Fallback to solr search API if metadata fetch fails
        }

        return "RELEASE";
    }

    public async Task<string> DownloadArtifactAsync(
        MavenArtifactCoordinate coord,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        var resolved = coord;
        if (coord.Version.Equals("LATEST", StringComparison.OrdinalIgnoreCase) ||
            coord.Version.Equals("RELEASE", StringComparison.OrdinalIgnoreCase))
        {
            progress?.Invoke($"🔍 Resolving latest version for {coord.GroupId}:{coord.ArtifactId}...\n");
            var latestVer = await ResolveLatestVersionAsync(coord.GroupId, coord.ArtifactId, ct).ConfigureAwait(false);
            resolved = coord with { Version = latestVer };
        }

        var localJar = GetLocalJarPath(resolved);
        if (_host.FileExists(localJar))
        {
            progress?.Invoke($"✔ Using cached {resolved.Canonical} ({localJar})\n");
            return localJar;
        }

        var jarUrl = $"{CentralRepoBase}/{resolved.GroupPath}/{resolved.ArtifactId}/{resolved.Version}/{resolved.FileName}";
        progress?.Invoke($"⬇ Downloading {resolved.Canonical} from Maven Central...\n");

        var dir = Path.GetDirectoryName(localJar)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var response = await _http.GetAsync(jarUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Maven artifact not found: {jarUrl} (HTTP {response.StatusCode})");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(localJar, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, ct).ConfigureAwait(false);

        progress?.Invoke($"✔ Saved {resolved.FileName} to {localJar}\n");
        return localJar;
    }

    public async Task<IReadOnlyList<MavenSearchResultItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<MavenSearchResultItem>();

        var url = $"{SearchApiBase}?q={Uri.EscapeDataString(query.Trim())}&rows=20&wt=json";
        try
        {
            var response = await _http.GetFromJsonAsync<MavenSearchResponse>(url, ct).ConfigureAwait(false);
            return response?.Response?.Docs ?? (IReadOnlyList<MavenSearchResultItem>)Array.Empty<MavenSearchResultItem>();
        }
        catch
        {
            return Array.Empty<MavenSearchResultItem>();
        }
    }

    private sealed class MavenSearchResponse
    {
        [JsonPropertyName("response")]
        public MavenSearchResponseBody? Response { get; set; }
    }

    private sealed class MavenSearchResponseBody
    {
        [JsonPropertyName("docs")]
        public List<MavenSearchResultItem> Docs { get; set; } = new();
    }
}
