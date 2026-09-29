using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public sealed record NuGetPackageItem(
    string Id,
    string Version,
    string Description,
    string Authors,
    long TotalDownloads,
    string IconUrl = "");

public partial class CSharpCodeStudioViewModel
{
    private static readonly HttpClient NuGetHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private static readonly NuGetPackageItem[] PopularNuGetPackages =
    [
        new("Newtonsoft.Json", "13.0.3", "Popular high-performance JSON framework for .NET", "James Newton-King", 5000000000),
        new("SkiaSharp", "3.119.4", "Cross-platform 2D graphics API based on Google Skia", "Microsoft", 200000000),
        new("ScottPlot", "5.0.55", "Interactive plotting library for .NET", "Scott W Harden", 25000000),
        new("CsvHelper", "33.0.1", "Fast library for reading and writing CSV files", "Josh Close", 650000000),
        new("YamlDotNet", "16.3.0", "Complete YAML parser and emitter library for .NET", "Antoine Aubry", 600000000),
        new("Dapper", "2.1.66", "Simple, high-performance object mapper for .NET", "Stack Exchange", 600000000),
        new("RestSharp", "112.1.0", "Simple REST and HTTP API Client for .NET", "John Sheehan, Andrew Young", 450000000),
        new("Spectre.Console", "0.49.1", "Beautiful console application formatting and tables", "Patrik Svensson", 120000000),
        new("Humanizer", "2.14.1", "Manipulate and display strings, enums, dates, times", "Mehdi Khalili", 900000000),
        new("Polly", "8.5.2", "Resilience and transient-fault-handling library", "Michael Wolfenden, App vNext", 800000000),
        new("Serilog", "4.2.0", "Simple, flexible diagnostic logging library", "Serilog Contributors", 1200000000),
        new("FluentValidation", "11.11.0", "Popular validation library for building strongly-typed rules", "Jeremy Skinner", 900000000)
    ];

    private static readonly NuGetPackageItem[] PopularJavaPackages =
    [
        new("com.google.code.gson:gson", "2.11.0", "Google JSON library for Java", "Google", 100000000),
        new("com.fasterxml.jackson.core:jackson-databind", "2.17.0", "General data-binding functionality for Jackson", "FasterXML", 200000000),
        new("org.apache.commons:commons-lang3", "3.14.0", "Java standard library extensions", "Apache", 300000000),
        new("commons-io:commons-io", "2.16.1", "Utility classes, stream implementations, file filters", "Apache", 250000000),
        new("com.google.guava:guava", "33.1.0-jre", "Google Core Libraries for Java", "Google", 250000000),
        new("org.slf4j:slf4j-api", "2.0.12", "The slf4j logging API", "QOS.ch", 500000000),
        new("org.xerial:sqlite-jdbc", "3.45.1.0", "SQLite JDBC Driver", "Taro L. Saito", 50000000),
        new("org.knowm.xchart:xchart", "3.8.7", "Light-weight charting library for Java", "Knowm", 5000000)
    ];

    private static readonly NuGetPackageItem[] PopularCppPackages =
    [
        new("nlohmann-json", "3.11.3", "JSON for Modern C++", "Niels Lohmann", 50000000),
        new("fmt", "10.2.1", "Fast and safe alternative to C stdio and C++ iostreams", "Victor Zverovich", 80000000),
        new("spdlog", "1.13.0", "Fast C++ logging library", "Gabi Melman", 40000000),
        new("cxxopts", "3.2.0", "Lightweight C++ command line option parser", "Jarryd Beck", 15000000),
        new("eigen3", "3.4.0", "C++ template library for linear algebra", "Benoît Jacob, Gaël Guennebaud", 30000000),
        new("boost", "1.85.0", "Peer-reviewed portable C++ source libraries", "Boost Contributors", 100000000),
        new("catch2", "3.5.4", "Modern, C++-native test framework", "Phil Nash", 20000000),
        new("opencv4", "4.9.0", "Open Source Computer Vision Library", "OpenCV team", 25000000)
    ];

    private static readonly NuGetPackageItem[] PopularGoPackages =
    [
        new("github.com/gin-gonic/gin", "1.10.0", "Gin is a HTTP web framework written in Go", "Gin Authors", 120000000),
        new("github.com/google/uuid", "1.6.0", "Generates and inspects UUIDs based on RFC 4122", "Google", 200000000),
        new("github.com/stretchr/testify", "1.9.0", "A toolkit with common assertions and mocks for Go", "Stretchr", 150000000),
        new("github.com/spf13/cobra", "1.8.1", "A Commander for modern Go CLI interactions", "Steve Francia", 90000000),
        new("gorm.io/gorm", "1.25.10", "The fantastic ORM library for Golang", "Jinzhu", 80000000),
        new("go.uber.org/zap", "1.27.0", "Blazing fast, structured, leveled logging in Go", "Uber", 70000000),
        new("golang.org/x/sync", "0.7.0", "Go concurrency primitives such as errgroup", "Go Authors", 180000000),
        new("gopkg.in/yaml.v3", "3.0.1", "YAML support for the Go language", "Canonical", 140000000)
    ];

    public string ActivePackageManagerName => ActiveLanguage?.Packages?.ToolName ?? "NuGet";
    public string ActivePackageManagerTitle => $"{ActivePackageManagerName.ToUpperInvariant()} PACKAGES";
    public string ActivePackageSearchPlaceholder => $"Search {ActivePackageManagerName} packages...";
    public string ActiveScriptDirectivesTitle => $"{ActiveLanguage?.DisplayName.ToUpperInvariant() ?? "SCRIPT"} DIRECTIVES";

    [ObservableProperty]
    private string _nuGetSearchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearchingNuGet;

    [ObservableProperty]
    private string? _nuGetStatusMessage;

    public ObservableCollection<NuGetPackageItem> NuGetSearchResults { get; } = new();

    public ObservableCollection<string> DocumentNuGetPackages { get; } = new();

    public NuGetPackageItem[] GetPopularPackagesForActiveLanguage()
    {
        var id = ActiveLanguage?.Id;
        if (string.Equals(id, Services.Languages.LanguageIds.Java, StringComparison.OrdinalIgnoreCase))
            return PopularJavaPackages;
        if (string.Equals(id, Services.Languages.LanguageIds.Cpp, StringComparison.OrdinalIgnoreCase))
            return PopularCppPackages;
        if (string.Equals(id, Services.Languages.LanguageIds.Go, StringComparison.OrdinalIgnoreCase))
            return PopularGoPackages;
        return PopularNuGetPackages;
    }

    public void InitializeNuGetPackages()
    {
        RefreshDocumentNuGetPackages();
        NuGetSearchResults.Clear();
        foreach (var p in GetPopularPackagesForActiveLanguage())
        {
            NuGetSearchResults.Add(p);
        }
        OnPropertyChanged(nameof(ActivePackageManagerName));
        OnPropertyChanged(nameof(ActivePackageManagerTitle));
        OnPropertyChanged(nameof(ActivePackageSearchPlaceholder));
        OnPropertyChanged(nameof(ActiveScriptDirectivesTitle));
    }

    [RelayCommand]
    public async Task SearchNuGetPackagesAsync()
    {
        var query = NuGetSearchQuery.Trim();
        var popular = GetPopularPackagesForActiveLanguage();
        if (string.IsNullOrWhiteSpace(query))
        {
            NuGetSearchResults.Clear();
            foreach (var p in popular)
            {
                NuGetSearchResults.Add(p);
            }
            NuGetStatusMessage = "Displaying popular packages";
            return;
        }

        // C++ package search
        if (string.Equals(ActiveLanguage?.Id, Services.Languages.LanguageIds.Cpp, StringComparison.OrdinalIgnoreCase))
        {
            NuGetSearchResults.Clear();
            var filtered = Array.FindAll(PopularCppPackages, p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
            foreach (var p in filtered) NuGetSearchResults.Add(p);
            NuGetStatusMessage = filtered.Length > 0 ? $"Found {filtered.Length} matching packages" : "No packages found in catalog";
            return;
        }

        // Go package search
        if (string.Equals(ActiveLanguage?.Id, Services.Languages.LanguageIds.Go, StringComparison.OrdinalIgnoreCase))
        {
            NuGetSearchResults.Clear();
            var filtered = Array.FindAll(PopularGoPackages, p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
            foreach (var p in filtered) NuGetSearchResults.Add(p);
            NuGetStatusMessage = filtered.Length > 0 ? $"Found {filtered.Length} matching packages" : "No packages found in catalog";
            return;
        }

        // Java Maven search
        if (string.Equals(ActiveLanguage?.Id, Services.Languages.LanguageIds.Java, StringComparison.OrdinalIgnoreCase))
        {
            IsSearchingNuGet = true;
            NuGetStatusMessage = $"Searching Maven Central for '{query}'...";

            try
            {
                var resolver = new Services.Languages.Java.MavenCentralResolver(new Services.Toolchains.HostEnvironment());
                var docs = await resolver.SearchAsync(query);

                NuGetSearchResults.Clear();
                if (docs.Count > 0)
                {
                    foreach (var doc in docs)
                    {
                        NuGetSearchResults.Add(new NuGetPackageItem(
                            $"{doc.GroupId}:{doc.ArtifactId}",
                            doc.LatestVersion ?? "1.0.0",
                            $"Maven coordinate: {doc.GroupId}:{doc.ArtifactId}",
                            doc.GroupId,
                            doc.VersionCount * 1000));
                    }
                    NuGetStatusMessage = $"Found {docs.Count} packages on Maven Central";
                }
                else
                {
                    var filtered = Array.FindAll(PopularJavaPackages, p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
                    foreach (var p in filtered) NuGetSearchResults.Add(p);
                    NuGetStatusMessage = filtered.Length > 0 ? $"Found {filtered.Length} matching packages" : "No packages found on Maven Central";
                }
            }
            catch
            {
                NuGetSearchResults.Clear();
                var filtered = Array.FindAll(PopularJavaPackages, p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
                foreach (var p in filtered) NuGetSearchResults.Add(p);
                NuGetStatusMessage = "Network unavailable: showing offline packages";
            }
            finally
            {
                IsSearchingNuGet = false;
            }
            return;
        }

        // C# NuGet search
        IsSearchingNuGet = true;
        NuGetStatusMessage = $"Searching NuGet for '{query}'...";

        try
        {
            var url = $"https://azuresearch-usnc.nuget.org/query?q={Uri.EscapeDataString(query)}&take=12&prerelease=false";
            var response = await NuGetHttpClient.GetFromJsonAsync<NuGetSearchResponse>(url);

            NuGetSearchResults.Clear();
            if (response?.Data != null && response.Data.Count > 0)
            {
                foreach (var item in response.Data)
                {
                    var authors = item.Authors != null && item.Authors.Count > 0 ? string.Join(", ", item.Authors) : (item.Id ?? string.Empty);
                    NuGetSearchResults.Add(new NuGetPackageItem(
                        item.Id ?? "Unknown",
                        item.Version ?? "1.0.0",
                        item.Description ?? "No description provided.",
                        authors,
                        item.TotalDownloads,
                        item.IconUrl ?? string.Empty));
                }
                NuGetStatusMessage = $"Found {response.Data.Count} packages";
            }
            else
            {
                var filtered = Array.FindAll(PopularNuGetPackages, p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
                foreach (var p in filtered) NuGetSearchResults.Add(p);
                NuGetStatusMessage = filtered.Length > 0 ? $"Found {filtered.Length} matching packages" : "No packages found on NuGet";
            }
        }
        catch
        {
            NuGetSearchResults.Clear();
            var filtered = Array.FindAll(PopularNuGetPackages, p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
            foreach (var p in filtered) NuGetSearchResults.Add(p);
            NuGetStatusMessage = "Network unavailable: showing offline packages";
        }
        finally
        {
            IsSearchingNuGet = false;
        }
    }

    [RelayCommand]
    public void AddNuGetPackage(NuGetPackageItem? package)
    {
        if (package == null) return;

        var langId = ActiveLanguage?.Id;
        string directive = langId switch
        {
            Services.Languages.LanguageIds.Java => $"//DEPS {package.Id}:{package.Version}",
            Services.Languages.LanguageIds.Cpp => $"// #vcpkg: {package.Id}",
            Services.Languages.LanguageIds.Go => $"// #go: {package.Id}",
            Services.Languages.LanguageIds.Python => $"%pip install {package.Id}",
            Services.Languages.LanguageIds.JavaScript => $"%npm install {package.Id}",
            _ => $"#r \"nuget: {package.Id}, {package.Version}\""
        };

        if (Code.Contains(directive, StringComparison.OrdinalIgnoreCase))
        {
            NuGetStatusMessage = $"'{package.Id}' is already referenced in script.";
            return;
        }

        // Insert at the top of the editor code
        Code = directive + Environment.NewLine + Code;
        RefreshDocumentNuGetPackages();
        NuGetStatusMessage = $"Added {package.Id} to script references!";
    }

    [RelayCommand]
    public void RemoveNuGetPackage(string? packageDirective)
    {
        if (string.IsNullOrWhiteSpace(packageDirective)) return;

        var escaped = Regex.Escape(packageDirective.Trim());
        var regex = new Regex($@"^\s*{escaped}\s*;?\r?\n?", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Code = regex.Replace(Code, string.Empty);
        RefreshDocumentNuGetPackages();
        NuGetStatusMessage = $"Removed package reference from script.";
    }

    [RelayCommand]
    public void RefreshDocumentNuGetPackages()
    {
        DocumentNuGetPackages.Clear();
        if (string.IsNullOrEmpty(Code)) return;

        var regex = new Regex(@"^\s*(?:#r\s+""nuget:[^""]+""|//\s*DEPS\s+[^\r\n]+|//\s*#(?:vcpkg|pkg):[^\r\n]+|[%#!](?:pip3?|npm|vcpkg|maven)\s+[^\r\n]+)\s*;?", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var matches = regex.Matches(Code);
        foreach (Match m in matches)
        {
            var line = m.Value.Trim();
            if (!DocumentNuGetPackages.Contains(line))
            {
                DocumentNuGetPackages.Add(line);
            }
        }
    }

    private sealed class NuGetSearchResponse
    {
        [JsonPropertyName("totalHits")]
        public int TotalHits { get; set; }

        [JsonPropertyName("data")]
        public System.Collections.Generic.List<NuGetSearchData>? Data { get; set; }
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
        public System.Collections.Generic.List<string>? Authors { get; set; }

        [JsonPropertyName("totalDownloads")]
        public long TotalDownloads { get; set; }

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; set; }
    }
}
