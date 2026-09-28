using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;

/// <summary>
/// Provides NuGet package resolution, directive parsing, and installation for C# scripts and notebook cells.
/// Supports inline <c>#r "nuget: ..."</c> directives, <c>%nuget install ...</c> magic commands,
/// and automated dependency quick-fixes for unresolved types (CS0246).
/// </summary>
public sealed partial class NuGetPackageManager : IPackageManager
{
    private readonly NuGetReferenceResolver _resolver;

    [GeneratedRegex(@"^\s*#r\s+""nuget:\s*(?<id>[a-zA-Z0-9_\-\.]+)(?:,\s*(?<ver>[^""]+))?""\s*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex NuGetDirectiveRegex();

    [GeneratedRegex(@"^[%#!]nuget\s+(?:install|add)\s+(?<id>[a-zA-Z0-9_\-\.]+)(?:\s+(?<ver>[^\s]+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NuGetMagicRegex();

    [GeneratedRegex(@"^dotnet\s+add\s+package\s+(?<id>[a-zA-Z0-9_\-\.]+)(?:\s+(?:-v|--version)\s+(?<ver>[^\s]+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex DotNetAddPackageRegex();

    private static readonly Dictionary<string, string> KnownTypeToPackageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JObject"] = "Newtonsoft.Json",
        ["JArray"] = "Newtonsoft.Json",
        ["JToken"] = "Newtonsoft.Json",
        ["JsonConvert"] = "Newtonsoft.Json",
        ["SKBitmap"] = "SkiaSharp",
        ["SKCanvas"] = "SkiaSharp",
        ["SKPaint"] = "SkiaSharp",
        ["SKSurface"] = "SkiaSharp",
        ["SKImage"] = "SkiaSharp",
        ["SKColor"] = "SkiaSharp",
        ["Plot"] = "ScottPlot",
        ["ScottPlot"] = "ScottPlot",
        ["CsvReader"] = "CsvHelper",
        ["CsvWriter"] = "CsvHelper",
        ["YamlDotNet"] = "YamlDotNet",
        ["SerializerBuilder"] = "YamlDotNet",
        ["DeserializerBuilder"] = "YamlDotNet",
        ["Dapper"] = "Dapper",
        ["RestClient"] = "RestSharp",
        ["RestRequest"] = "RestSharp",
        ["AnsiConsole"] = "Spectre.Console",
        ["Table"] = "Spectre.Console",
        ["Humanizer"] = "Humanizer",
        ["Polly"] = "Polly",
        ["Serilog"] = "Serilog",
        ["Log"] = "Serilog",
        ["NLog"] = "NLog",
        ["FluentValidation"] = "FluentValidation",
        ["AbstractValidator"] = "FluentValidation",
        ["AutoMapper"] = "AutoMapper",
        ["IMapper"] = "AutoMapper",
        ["MediatR"] = "MediatR",
        ["IMediator"] = "MediatR",
        ["BenchmarkDotNet"] = "BenchmarkDotNet",
        ["Benchmark"] = "BenchmarkDotNet",
        ["SixLabors.ImageSharp"] = "SixLabors.ImageSharp",
        ["Image"] = "SixLabors.ImageSharp",
        ["DbContext"] = "Microsoft.EntityFrameworkCore",
        ["Npgsql"] = "Npgsql",
        ["DnsClient"] = "DnsClient",
        ["HtmlAgilityPack"] = "HtmlAgilityPack",
        ["HtmlDocument"] = "HtmlAgilityPack",
    };

    public NuGetPackageManager(NuGetReferenceResolver? resolver = null)
    {
        _resolver = resolver ?? new NuGetReferenceResolver();
    }

    public string ToolName => "NuGet";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var trimmed = line.Trim();
        var matchDirective = NuGetDirectiveRegex().Match(trimmed);
        if (matchDirective.Success)
        {
            var id = matchDirective.Groups["id"].Value;
            var ver = matchDirective.Groups["ver"].Success ? matchDirective.Groups["ver"].Value.Trim() : null;
            var args = ver != null ? new[] { "install", id, ver } : new[] { "install", id };
            command = new PackageCommand(trimmed, args);
            return true;
        }

        var matchMagic = NuGetMagicRegex().Match(trimmed);
        if (matchMagic.Success)
        {
            var id = matchMagic.Groups["id"].Value;
            var ver = matchMagic.Groups["ver"].Success ? matchMagic.Groups["ver"].Value.Trim() : null;
            var args = ver != null ? new[] { "install", id, ver } : new[] { "install", id };
            command = new PackageCommand(trimmed, args);
            return true;
        }

        var matchDotnet = DotNetAddPackageRegex().Match(trimmed);
        if (matchDotnet.Success)
        {
            var id = matchDotnet.Groups["id"].Value;
            var ver = matchDotnet.Groups["ver"].Success ? matchDotnet.Groups["ver"].Value.Trim() : null;
            var args = ver != null ? new[] { "install", id, ver } : new[] { "install", id };
            command = new PackageCommand(trimmed, args);
            return true;
        }

        command = null!;
        return false;
    }

    public PackageCommand InstallCommand(string package)
    {
        var trimmed = package.Trim();
        return new PackageCommand($"#r \"nuget: {trimmed}\"", ["install", trimmed]);
    }

    public string PackageForMissingDependency(string missingName)
    {
        if (string.IsNullOrWhiteSpace(missingName)) return missingName;

        var clean = missingName.Trim();
        var dot = clean.LastIndexOf('.');
        if (dot >= 0 && dot < clean.Length - 1)
        {
            clean = clean[(dot + 1)..];
        }

        if (KnownTypeToPackageMap.TryGetValue(clean, out var pkg))
        {
            return pkg;
        }

        if (KnownTypeToPackageMap.TryGetValue(missingName.Trim(), out var fullPkg))
        {
            return fullPkg;
        }

        return missingName.Trim();
    }

    public async Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default)
    {
        if (command.Arguments.Count < 2)
        {
            return new PackageCommandResult(false, "Specify a NuGet package to install, e.g. #r \"nuget: Newtonsoft.Json, 13.0.3\"");
        }

        var packageId = command.Arguments[1];
        var version = command.Arguments.Count > 2 && !string.IsNullOrWhiteSpace(command.Arguments[2])
            ? command.Arguments[2].Trim()
            : null;

        var directive = version != null
            ? $"#r \"nuget: {packageId}, {version}\""
            : $"#r \"nuget: {packageId}\"";

        output($"▶ Resolving NuGet package: {packageId} {(version != null ? "v" + version : "")}\n");

        try
        {
            var result = await _resolver.ProcessDirectivesAsync(directive, ct);
            foreach (var msg in result.Messages)
            {
                output(msg + "\n");
            }

            var hasError = result.Messages.Any(m => m.Contains("failed", StringComparison.OrdinalIgnoreCase) || m.Contains("⚠️"));
            if (hasError && result.References.Count == 0)
            {
                return new PackageCommandResult(false, $"Failed to restore NuGet package '{packageId}'.");
            }

            return new PackageCommandResult(true, $"Successfully restored {packageId}.");
        }
        catch (OperationCanceledException)
        {
            return new PackageCommandResult(false, "NuGet package restore cancelled.");
        }
        catch (Exception ex)
        {
            output($"❌ Error resolving package '{packageId}': {ex.Message}\n");
            return new PackageCommandResult(false, ex.Message);
        }
    }
}
