using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class NuGetResolutionResult
{
    public string SanitizedCode { get; set; } = string.Empty;
    public List<MetadataReference> References { get; } = new();
    public List<string> LoadedAssemblies { get; } = new();
    public List<string> Messages { get; } = new();
}

public class NuGetReferenceResolver
{
    private static readonly Regex NuGetDirectiveRegex = new(
        @"^\s*#r\s+""nuget:\s*([a-zA-Z0-9_\-\.]+)(?:,\s*([^""]+))?""\s*;?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private readonly string _globalPackagesFolder;
    private static readonly object _resolverLock = new();

    public NuGetReferenceResolver()
    {
        var envFolder = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(envFolder) && Directory.Exists(envFolder))
        {
            _globalPackagesFolder = envFolder;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _globalPackagesFolder = Path.Combine(home, ".nuget", "packages");
        }
    }

    public NuGetResolutionResult ProcessDirectives(string code)
    {
        var result = new NuGetResolutionResult();
        var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sanitizedLines = new List<string>();

        foreach (var line in lines)
        {
            var match = NuGetDirectiveRegex.Match(line);
            if (match.Success)
            {
                var packageId = match.Groups[1].Value.Trim();
                var requestedVersion = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;

                ResolvePackage(packageId, requestedVersion, result);

                // Preserve line numbering by commenting out the directive
                sanitizedLines.Add("// " + line.TrimStart());
            }
            else
            {
                sanitizedLines.Add(line);
            }
        }

        result.SanitizedCode = string.Join(Environment.NewLine, sanitizedLines);
        return result;
    }

    private void ResolvePackage(string packageId, string? requestedVersion, NuGetResolutionResult result)
    {
        if (!Directory.Exists(_globalPackagesFolder))
        {
            result.Messages.Add($"⚠️ NuGet cache not found at: {_globalPackagesFolder}");
            return;
        }

        var packageDir = Path.Combine(_globalPackagesFolder, packageId.ToLowerInvariant());
        if (!Directory.Exists(packageDir))
        {
            result.Messages.Add($"⚠️ Package '{packageId}' not found in local NuGet cache.");
            return;
        }

        string? selectedVersionDir = null;

        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            var exactPath = Path.Combine(packageDir, requestedVersion);
            if (Directory.Exists(exactPath))
            {
                selectedVersionDir = exactPath;
            }
            else
            {
                // Try case-insensitive or nearest match
                var availableVersions = Directory.GetDirectories(packageDir);
                selectedVersionDir = availableVersions.FirstOrDefault(d =>
                    string.Equals(Path.GetFileName(d), requestedVersion, StringComparison.OrdinalIgnoreCase));

                if (selectedVersionDir == null && availableVersions.Length > 0)
                {
                    // Fallback to highest available version
                    selectedVersionDir = availableVersions.OrderByDescending(d => Path.GetFileName(d)).First();
                    var fallbackVersion = Path.GetFileName(selectedVersionDir);
                    result.Messages.Add($"ℹ️ Exact version '{requestedVersion}' for '{packageId}' not in cache; using cached '{fallbackVersion}'.");
                }
            }
        }
        else
        {
            var availableVersions = Directory.GetDirectories(packageDir);
            if (availableVersions.Length > 0)
            {
                selectedVersionDir = availableVersions.OrderByDescending(d => Path.GetFileName(d)).First();
            }
        }

        if (selectedVersionDir == null)
        {
            result.Messages.Add($"⚠️ No suitable version found for package '{packageId}'.");
            return;
        }

        var activeVersion = Path.GetFileName(selectedVersionDir);
        var libDir = Path.Combine(selectedVersionDir, "lib");

        if (Directory.Exists(libDir))
        {
            var targetTfms = new[]
            {
                "net10.0", "net9.0", "net8.0", "net7.0", "net6.0",
                "netstandard2.1", "netstandard2.0"
            };

            string? matchedTfmDir = null;
            foreach (var tfm in targetTfms)
            {
                var candidate = Path.Combine(libDir, tfm);
                if (Directory.Exists(candidate))
                {
                    matchedTfmDir = candidate;
                    break;
                }
            }

            if (matchedTfmDir == null)
            {
                // Grab first available subfolder under lib/
                matchedTfmDir = Directory.GetDirectories(libDir).FirstOrDefault();
            }

            if (matchedTfmDir != null)
            {
                var dllFiles = Directory.GetFiles(matchedTfmDir, "*.dll");
                foreach (var dll in dllFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileName(dll);
                        result.References.Add(MetadataReference.CreateFromFile(dll));
                        result.LoadedAssemblies.Add($"{packageId} ({activeVersion}) -> {fileName}");
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add($"⚠️ Error referencing {dll}: {ex.Message}");
                    }
                }
            }
        }

        // Handle native assets for known libraries like SkiaSharp
        ResolveNativeAssetsIfAny(packageId, activeVersion, result);
    }

    private void ResolveNativeAssetsIfAny(string packageId, string version, NuGetResolutionResult result)
    {
        lock (_resolverLock)
        {
            try
            {
                if (packageId.Equals("SkiaSharp", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureSkiaSharpNativeLoaded(version, result);
                }
            }
            catch (Exception ex)
            {
                result.Messages.Add($"⚠️ Native library resolution warning: {ex.Message}");
            }
        }
    }

    private void EnsureSkiaSharpNativeLoaded(string version, NuGetResolutionResult result)
    {
        // Check if native dylib/so/dll can be located
        string? nativeLibName = null;
        string? nativePackageSuffix = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            nativeLibName = "libSkiaSharp.dylib";
            nativePackageSuffix = "skiasharp.nativeassets.macos";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            nativeLibName = "libSkiaSharp.so";
            nativePackageSuffix = "skiasharp.nativeassets.linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nativeLibName = "libSkiaSharp.dll";
            nativePackageSuffix = "skiasharp.nativeassets.win32";
        }

        if (nativeLibName == null || nativePackageSuffix == null) return;

        var nativePkgDir = Path.Combine(_globalPackagesFolder, nativePackageSuffix);
        if (!Directory.Exists(nativePkgDir)) return;

        var availableVersions = Directory.GetDirectories(nativePkgDir);
        var targetDir = availableVersions.FirstOrDefault(d => string.Equals(Path.GetFileName(d), version, StringComparison.OrdinalIgnoreCase))
            ?? availableVersions.OrderByDescending(d => Path.GetFileName(d)).FirstOrDefault();

        if (targetDir == null) return;

        var matchingFiles = Directory.GetFiles(targetDir, nativeLibName, SearchOption.AllDirectories);
        var candidate = matchingFiles.FirstOrDefault(f =>
            !f.Contains("tvos", StringComparison.OrdinalIgnoreCase) &&
            !f.Contains("ios", StringComparison.OrdinalIgnoreCase) &&
            !f.Contains("android", StringComparison.OrdinalIgnoreCase));

        if (candidate != null && File.Exists(candidate))
        {
            try
            {
                NativeLibrary.Load(candidate);
                result.Messages.Add($"✨ Loaded native graphics runtime: {Path.GetFileName(candidate)}");
            }
            catch
            {
                // May already be loaded in host process
            }
        }
    }
}
