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

    [ObservableProperty]
    private string _nuGetSearchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearchingNuGet;

    [ObservableProperty]
    private string? _nuGetStatusMessage;

    public ObservableCollection<NuGetPackageItem> NuGetSearchResults { get; } = new();

    public ObservableCollection<string> DocumentNuGetPackages { get; } = new();

    public void InitializeNuGetPackages()
    {
        RefreshDocumentNuGetPackages();
        if (NuGetSearchResults.Count == 0)
        {
            foreach (var p in PopularNuGetPackages)
            {
                NuGetSearchResults.Add(p);
            }
        }
    }

    [RelayCommand]
    public async Task SearchNuGetPackagesAsync()
    {
        var query = NuGetSearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            NuGetSearchResults.Clear();
            foreach (var p in PopularNuGetPackages)
            {
                NuGetSearchResults.Add(p);
            }
            NuGetStatusMessage = "Displaying popular packages";
            return;
        }

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
                // Fallback to local filtering of popular packages
                var filtered = Array.FindAll(PopularNuGetPackages, p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
                foreach (var p in filtered)
                {
                    NuGetSearchResults.Add(p);
                }
                NuGetStatusMessage = filtered.Length > 0
                    ? $"Found {filtered.Length} matching package{(filtered.Length == 1 ? "" : "s")}"
                    : "No packages found on NuGet";
            }
        }
        catch
        {
            // Offline fallback
            NuGetSearchResults.Clear();
            var filtered = Array.FindAll(PopularNuGetPackages, p => p.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
            foreach (var p in filtered)
            {
                NuGetSearchResults.Add(p);
            }
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

        var directive = $"#r \"nuget: {package.Id}, {package.Version}\"";
        if (Code.Contains(directive, StringComparison.OrdinalIgnoreCase))
        {
            NuGetStatusMessage = $"'{package.Id}' is already referenced in script.";
            return;
        }

        // Insert at the top of the editor code
        Code = directive + Environment.NewLine + Code;
        RefreshDocumentNuGetPackages();
        NuGetStatusMessage = $"Added {package.Id} v{package.Version} to script references!";
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
        var regex = new Regex(@"^\s*#r\s+""nuget:\s*([a-zA-Z0-9_\-\.]+)(?:,\s*([^""]+))?""\s*;?", RegexOptions.Multiline | RegexOptions.IgnoreCase);
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
