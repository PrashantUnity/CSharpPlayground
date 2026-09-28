using System.Text;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;

/// <summary>
/// Compiles and runs C# code through the external <c>dotnet</c> CLI via a two-phase <see cref="ScriptRunPlan"/>:
/// Phase 1 (Build step): <c>dotnet build -nologo -c Release App.csproj</c> (stops on compiler error).
/// Phase 2 (Run step): <c>dotnet run --no-build -nologo -c Release --project App.csproj</c> with interactive terminal input streaming.
/// Automatically bridges inline <c>#r "nuget: ..."</c> directives into MSBuild PackageReferences.
/// </summary>
public sealed partial class CSharpBuildAndRunScriptRunner(IHostEnvironment host) : IScriptRunner
{
    [GeneratedRegex(@"^\s*#r\s+""nuget:\s*(?<id>[a-zA-Z0-9_\-\.]+)(?:,\s*(?<ver>[^""]+))?""\s*;?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex NuGetDirectiveRegex();

    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var dotnetPath = context.Toolchain.ExecutablePath;
        var sourceCode = string.Empty;

        if (host.FileExists(context.SourceFilePath))
        {
            try
            {
                sourceCode = await File.ReadAllTextAsync(context.SourceFilePath, ct);
            }
            catch
            {
                // Fallback to empty if read fails
            }
        }

        // Isolated compilation folder based on source file path hash
        var hash = Math.Abs(context.SourceFilePath.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "csharp_build", hash);
        try
        {
            Directory.CreateDirectory(outDir);
        }
        catch
        {
            // Directory creation issues reported by dotnet if unwritable
        }

        // Parse #r "nuget: ..." directives into PackageReferences
        var packageRefs = new StringBuilder();
        var sanitizedCode = NuGetDirectiveRegex().Replace(sourceCode, m =>
        {
            var id = m.Groups["id"].Value;
            var ver = m.Groups["ver"].Success && !string.IsNullOrWhiteSpace(m.Groups["ver"].Value)
                ? m.Groups["ver"].Value.Trim()
                : null;

            if (ver != null)
            {
                packageRefs.AppendLine($"    <PackageReference Include=\"{id}\" Version=\"{ver}\" />");
            }
            else
            {
                packageRefs.AppendLine($"    <PackageReference Include=\"{id}\" Version=\"*\" />");
            }

            return $"// {m.Value.Trim()}";
        });

        // Stage Program.cs
        var stagedProgram = Path.Combine(outDir, "Program.cs");
        await File.WriteAllTextAsync(stagedProgram, sanitizedCode, Encoding.UTF8, ct).ConfigureAwait(false);

        // Stage App.csproj
        var stagedCsproj = Path.Combine(outDir, "App.csproj");
        var csprojContent = $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
{packageRefs}  </ItemGroup>
</Project>
""";
        await File.WriteAllTextAsync(stagedCsproj, csprojContent, Encoding.UTF8, ct).ConfigureAwait(false);

        var environment = await CSharpProcessEnvironment.ForAsync(host, context.Toolchain, ct);

        return new ScriptRunPlan(
        [
            new ProcessStep("Build", new ProcessStartSpec
            {
                FileName = dotnetPath,
                Arguments = ["build", "-nologo", "-c", "Release", stagedCsproj],
                WorkingDirectory = outDir,
                Environment = environment
            }, IsBuildStep: true),

            new ProcessStep("Run", new ProcessStartSpec
            {
                FileName = dotnetPath,
                Arguments = ["run", "--no-build", "-nologo", "-c", "Release", "--project", stagedCsproj],
                WorkingDirectory = context.WorkingDirectory,
                Environment = environment
            }, IsBuildStep: false)
        ]);
    }
}

/// <summary>Environment variables configured for external dotnet processes started by the studio.</summary>
public static class CSharpProcessEnvironment
{
    public static async Task<IReadOnlyDictionary<string, string?>> ForAsync(IHostEnvironment host, ToolchainInfo dotnet, CancellationToken ct = default)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NO_COLOR"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        };

        var path = await host.GetLoginShellPathAsync(ct) ?? host.GetEnvironmentVariable("PATH") ?? string.Empty;
        var bin = Path.GetDirectoryName(dotnet.ExecutablePath);
        if (!string.IsNullOrEmpty(bin))
        {
            path = bin + (host.IsWindows ? ";" : ":") + path;
        }

        if (path.Length > 0) environment["PATH"] = path;
        return environment;
    }
}
