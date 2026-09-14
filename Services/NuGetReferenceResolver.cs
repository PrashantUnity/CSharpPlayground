using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class NuGetResolutionResult
{
    public string SanitizedCode { get; set; } = string.Empty;
    public List<MetadataReference> References { get; } = new();
    public List<string> LoadedAssemblies { get; } = new();
    public List<string> Messages { get; } = new();
}

public sealed class NuGetSemanticVersion : IComparable<NuGetSemanticVersion>, IEquatable<NuGetSemanticVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public int Build { get; }
    public string? Prerelease { get; }
    public string OriginalString { get; }
    public bool IsPrerelease => !string.IsNullOrEmpty(Prerelease);

    public NuGetSemanticVersion(int major, int minor, int patch, int build, string? prerelease, string original)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Build = build;
        Prerelease = prerelease;
        OriginalString = original;
    }

    public static bool TryParse(string? versionStr, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out NuGetSemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(versionStr)) return false;

        var vStr = versionStr.Trim();
        var plusIdx = vStr.IndexOf('+');
        if (plusIdx >= 0) vStr = vStr.Substring(0, plusIdx);

        string? prerelease = null;
        var dashIdx = vStr.IndexOf('-');
        if (dashIdx >= 0)
        {
            prerelease = vStr.Substring(dashIdx + 1);
            vStr = vStr.Substring(0, dashIdx);
        }

        var parts = vStr.Split('.');
        int major = 0, minor = 0, patch = 0, build = 0;

        if (parts.Length > 0 && int.TryParse(parts[0], out var p0)) major = p0;
        else return false;

        if (parts.Length > 1 && int.TryParse(parts[1], out var p1)) minor = p1;
        if (parts.Length > 2 && int.TryParse(parts[2], out var p2)) patch = p2;
        if (parts.Length > 3 && int.TryParse(parts[3], out var p3)) build = p3;

        version = new NuGetSemanticVersion(major, minor, patch, build, prerelease, versionStr);
        return true;
    }

    public int CompareTo(NuGetSemanticVersion? other)
    {
        if (other is null) return 1;

        var cmp = Major.CompareTo(other.Major);
        if (cmp != 0) return cmp;

        cmp = Minor.CompareTo(other.Minor);
        if (cmp != 0) return cmp;

        cmp = Patch.CompareTo(other.Patch);
        if (cmp != 0) return cmp;

        cmp = Build.CompareTo(other.Build);
        if (cmp != 0) return cmp;

        if (!IsPrerelease && other.IsPrerelease) return 1;
        if (IsPrerelease && !other.IsPrerelease) return -1;

        if (IsPrerelease && other.IsPrerelease)
        {
            return string.Compare(Prerelease, other.Prerelease, StringComparison.OrdinalIgnoreCase);
        }

        return 0;
    }

    public bool Equals(NuGetSemanticVersion? other) => CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is NuGetSemanticVersion other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Build, Prerelease?.ToLowerInvariant());
    public override string ToString() => OriginalString;
}

public class NuGetReferenceResolver
{
    private static readonly Regex NuGetDirectiveRegex = new(
        @"^\s*#r\s+""nuget:\s*([a-zA-Z0-9_\-\.]+)(?:,\s*([^""]+))?""\s*;?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly string[] PreferredTfms =
    {
        "net10.0", "net9.0", "net8.0", "net7.0", "net6.0", "net5.0",
        "netcoreapp3.1", "netstandard2.1", "netstandard2.0"
    };

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly ConcurrentDictionary<Assembly, bool> ResolvedAssemblies = new();
    private static readonly object ResolverLock = new();

    private readonly string _globalPackagesFolder;

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
        return ProcessDirectivesAsync(code).GetAwaiter().GetResult();
    }

    public async Task<NuGetResolutionResult> ProcessDirectivesAsync(string code, CancellationToken ct = default)
    {
        var result = new NuGetResolutionResult();
        var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sanitizedLines = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var match = NuGetDirectiveRegex.Match(line);
            if (match.Success)
            {
                var packageId = match.Groups[1].Value.Trim();
                var requestedVersion = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;

                await ResolvePackageAsync(packageId, requestedVersion, result, visited, ct);

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

    private async Task ResolvePackageAsync(
        string packageId,
        string? requestedVersion,
        NuGetResolutionResult result,
        HashSet<string> visited,
        CancellationToken ct)
    {
        if (!visited.Add(packageId)) return;

        // 1. Generic Host Unification
        // If the host process already has the assembly loaded, prioritize host runtime compatibility
        // to avoid dual-identity ALC clashes or native symbol collision.
        var hostAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, packageId, StringComparison.OrdinalIgnoreCase));

        if (hostAssembly != null && !string.IsNullOrEmpty(hostAssembly.Location) && File.Exists(hostAssembly.Location))
        {
            var hostVer = hostAssembly.GetName().Version;
            var hostFileName = Path.GetFileName(hostAssembly.Location);

            if (!string.IsNullOrWhiteSpace(requestedVersion) &&
                NuGetSemanticVersion.TryParse(requestedVersion, out var reqSemVer) &&
                hostVer != null &&
                reqSemVer != null &&
                reqSemVer.Major != hostVer.Major)
            {
                result.Messages.Add($"ℹ️ Binding '{packageId}' to host runtime assembly (v{hostVer}) to ensure unified process state.");
            }
            else
            {
                result.Messages.Add($"✨ Using host runtime assembly: {packageId} v{hostVer}");
            }

            try
            {
                result.References.Add(MetadataReference.CreateFromFile(hostAssembly.Location));
                result.LoadedAssemblies.Add($"{packageId} ({hostVer}) -> {hostFileName}");
            }
            catch (Exception ex)
            {
                result.Messages.Add($"⚠️ Error referencing host {packageId}: {ex.Message}");
            }

            EnsureNativeAssetsResolved(hostAssembly, result);
            return;
        }

        // 2. Local NuGet Cache or On-Demand Download
        string? selectedVersionDir = null;
        var packageDir = Path.Combine(_globalPackagesFolder, packageId.ToLowerInvariant());

        if (Directory.Exists(packageDir))
        {
            selectedVersionDir = ResolveFromLocalCache(packageDir, requestedVersion, result);
        }

        if (selectedVersionDir == null)
        {
            // Attempt on-demand restore from NuGet v3 FlatContainer
            selectedVersionDir = await DownloadAndExtractPackageAsync(packageId, requestedVersion, result, ct);
        }

        if (selectedVersionDir == null)
        {
            result.Messages.Add($"⚠️ Package '{packageId}' could not be resolved or downloaded.");
            return;
        }

        var activeVersion = Path.GetFileName(selectedVersionDir);
        var libDir = Path.Combine(selectedVersionDir, "lib");
        Assembly? firstLoadedAssembly = null;

        if (Directory.Exists(libDir))
        {
            var matchedTfmDir = FindBestTfmDirectory(libDir);

            if (matchedTfmDir != null)
            {
                var dllFiles = Directory.GetFiles(matchedTfmDir, "*.dll");
                foreach (var dll in dllFiles)
                {
                    var fileName = Path.GetFileName(dll);
                    if (fileName.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        result.References.Add(MetadataReference.CreateFromFile(dll));
                        result.LoadedAssemblies.Add($"{packageId} ({activeVersion}) -> {fileName}");

                        if (firstLoadedAssembly == null)
                        {
                            try
                            {
                                firstLoadedAssembly = Assembly.LoadFrom(dll);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add($"⚠️ Error referencing {dll}: {ex.Message}");
                    }
                }
            }
        }

        // 3. Universal Native Asset Resolution
        if (firstLoadedAssembly != null)
        {
            EnsureNativeAssetsResolved(firstLoadedAssembly, result, selectedVersionDir);
        }

        // 4. Universal Transitive Dependency Resolution (.nuspec)
        await ResolveTransitiveDependenciesAsync(selectedVersionDir, packageId, result, visited, ct);
    }

    private string? ResolveFromLocalCache(string packageDir, string? requestedVersion, NuGetResolutionResult result)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            var exactPath = Path.Combine(packageDir, requestedVersion);
            if (Directory.Exists(exactPath)) return exactPath;

            var availableVersions = Directory.GetDirectories(packageDir);
            var matched = availableVersions.FirstOrDefault(d =>
                string.Equals(Path.GetFileName(d), requestedVersion, StringComparison.OrdinalIgnoreCase));

            if (matched != null) return matched;

            if (availableVersions.Length > 0)
            {
                var fallback = PickBestCompatibleVersion(availableVersions, requestedVersion);
                if (fallback != null)
                {
                    result.Messages.Add($"ℹ️ Requested '{requestedVersion}' for '{Path.GetFileName(packageDir)}' not cached; using '{Path.GetFileName(fallback)}'.");
                    return fallback;
                }
            }
        }
        else
        {
            var availableVersions = Directory.GetDirectories(packageDir);
            if (availableVersions.Length > 0)
            {
                return PickBestCompatibleVersion(availableVersions, null);
            }
        }

        return null;
    }

    private static string? PickBestCompatibleVersion(string[] availableVersionDirs, string? requestedVersion)
    {
        if (availableVersionDirs.Length == 0) return null;

        var parsed = availableVersionDirs
            .Select(d => (Dir: d, Version: NuGetSemanticVersion.TryParse(Path.GetFileName(d), out var v) ? v : null))
            .Where(x => x.Version != null)
            .Select(x => (x.Dir, Version: x.Version!))
            .ToList();

        if (parsed.Count == 0) return availableVersionDirs.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(requestedVersion) && NuGetSemanticVersion.TryParse(requestedVersion, out var reqSemVer))
        {
            var majorMatches = parsed.Where(x => x.Version.Major == reqSemVer!.Major).ToList();
            if (majorMatches.Count > 0)
            {
                var minorMatches = majorMatches.Where(x => x.Version.Minor == reqSemVer!.Minor).ToList();
                var pool = minorMatches.Count > 0 ? minorMatches : majorMatches;
                var stablePool = pool.Where(x => !x.Version.IsPrerelease).OrderByDescending(x => x.Version).ToList();
                if (stablePool.Count > 0) return stablePool[0].Dir;
                return pool.OrderByDescending(x => x.Version).FirstOrDefault().Dir;
            }
        }

        // When no version is requested, strictly prefer latest stable release over unreleased previews
        var stable = parsed.Where(x => !x.Version.IsPrerelease).OrderByDescending(x => x.Version).ToList();
        if (stable.Count > 0) return stable[0].Dir;

        return parsed.OrderByDescending(x => x.Version).FirstOrDefault().Dir;
    }

    private static string? FindBestTfmDirectory(string libDir)
    {
        foreach (var tfm in PreferredTfms)
        {
            var candidate = Path.Combine(libDir, tfm);
            if (Directory.Exists(candidate)) return candidate;
        }

        var subdirs = Directory.GetDirectories(libDir);
        var netDirs = subdirs.Where(d => Path.GetFileName(d).StartsWith("net", StringComparison.OrdinalIgnoreCase)).ToList();
        if (netDirs.Count > 0) return netDirs.OrderByDescending(d => Path.GetFileName(d)).FirstOrDefault();

        return subdirs.FirstOrDefault();
    }

    public void EnsureNativeAssetsResolved(Assembly assembly, NuGetResolutionResult? result = null, string? packageDir = null)
    {
        if (!ResolvedAssemblies.TryAdd(assembly, true)) return;

        lock (ResolverLock)
        {
            try
            {
                var asmName = assembly.GetName().Name ?? string.Empty;
                var asmVersion = assembly.GetName().Version;
                var activeVersion = !string.IsNullOrEmpty(packageDir) ? Path.GetFileName(packageDir) : null;
                var nativeLibs = DiscoverNativeLibraries(asmName, asmVersion, activeVersion, packageDir);

                if (nativeLibs.Count == 0) return;

                var handles = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);

                foreach (var libPath in nativeLibs)
                {
                    if (NativeLibrary.TryLoad(libPath, out var handle))
                    {
                        var fileName = Path.GetFileName(libPath);
                        var nameNoExt = Path.GetFileNameWithoutExtension(libPath);
                        var cleanName = nameNoExt.StartsWith("lib", StringComparison.OrdinalIgnoreCase)
                            ? nameNoExt.Substring(3)
                            : nameNoExt;

                        handles[fileName] = handle;
                        handles[nameNoExt] = handle;
                        handles[cleanName] = handle;
                    }
                }

                if (handles.Count > 0)
                {
                    try
                    {
                        NativeLibrary.SetDllImportResolver(assembly, (libraryName, targetAsm, searchPath) =>
                        {
                            if (handles.TryGetValue(libraryName, out var h)) return h;

                            var clean = libraryName;
                            if (clean.StartsWith("lib", StringComparison.OrdinalIgnoreCase))
                                clean = clean.Substring(3);

                            var dot = clean.IndexOf('.');
                            if (dot > 0) clean = clean.Substring(0, dot);

                            if (handles.TryGetValue(clean, out h)) return h;

                            return IntPtr.Zero;
                        });

                        result?.Messages.Add($"✨ Native runtime linked for {asmName}: [{string.Join(", ", handles.Keys.Distinct().Take(3))}]");
                    }
                    catch (InvalidOperationException)
                    {
                        // DllImportResolver already registered for this assembly
                    }
                }
            }
            catch (Exception ex)
            {
                result?.Messages.Add($"⚠️ Native resolution note for {assembly.GetName().Name}: {ex.Message}");
            }
        }
    }

    private List<string> DiscoverNativeLibraries(
        string asmName,
        Version? asmVersion,
        string? activeVersion,
        string? packageDir)
    {
        var discovered = new List<string>();
        string ext = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll" : ".so";

        var rids = GetPlatformRids();
        var osFolder = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "linux";

        var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Host Application Directories
        var hostBases = new[] { AppDomain.CurrentDomain.BaseDirectory, AppContext.BaseDirectory }.Distinct();
        foreach (var hb in hostBases)
        {
            if (string.IsNullOrEmpty(hb) || !Directory.Exists(hb)) continue;
            foreach (var rid in rids)
            {
                candidateDirs.Add(Path.Combine(hb, "runtimes", rid, "native"));
            }
            candidateDirs.Add(Path.Combine(hb, "runtimes", osFolder, "native"));

            var directMatches = Directory.GetFiles(hb, $"*{asmName}*{ext}*", SearchOption.TopDirectoryOnly);
            discovered.AddRange(directMatches);
        }

        // 2. Package Directory runtimes
        if (!string.IsNullOrEmpty(packageDir) && Directory.Exists(packageDir))
        {
            foreach (var rid in rids)
            {
                candidateDirs.Add(Path.Combine(packageDir, "runtimes", rid, "native"));
            }
            candidateDirs.Add(Path.Combine(packageDir, "runtimes", osFolder, "native"));
            candidateDirs.Add(Path.Combine(packageDir, "native"));
        }

        // 3. Satellite Native Packages in Global Cache (matched by active version or assembly version)
        if (Directory.Exists(_globalPackagesFolder))
        {
            var asmLower = asmName.ToLowerInvariant();
            var satelliteDirs = Directory.GetDirectories(_globalPackagesFolder)
                .Where(d =>
                {
                    var name = Path.GetFileName(d).ToLowerInvariant();
                    return name.Contains(asmLower) &&
                           (name.Contains("native") || name.Contains("runtime"));
                });

            foreach (var satPkg in satelliteDirs)
            {
                string? matchedVerDir = null;

                // Priority 1: Exact version match with active version
                if (!string.IsNullOrEmpty(activeVersion))
                {
                    var exact = Path.Combine(satPkg, activeVersion);
                    if (Directory.Exists(exact))
                    {
                        matchedVerDir = exact;
                    }
                }

                // Priority 2: Filter by assembly version (Major.Minor)
                if (matchedVerDir == null)
                {
                    var versions = Directory.GetDirectories(satPkg);
                    var versionFilter = activeVersion ?? (asmVersion != null ? $"{asmVersion.Major}.{asmVersion.Minor}" : null);
                    matchedVerDir = PickBestCompatibleVersion(versions, versionFilter);
                }

                if (matchedVerDir != null && Directory.Exists(matchedVerDir))
                {
                    foreach (var rid in rids)
                    {
                        candidateDirs.Add(Path.Combine(matchedVerDir, "runtimes", rid, "native"));
                    }
                    candidateDirs.Add(Path.Combine(matchedVerDir, "runtimes", osFolder, "native"));
                    candidateDirs.Add(Path.Combine(matchedVerDir, "native"));
                }
            }
        }

        foreach (var dir in candidateDirs)
        {
            if (!Directory.Exists(dir)) continue;

            var files = Directory.GetFiles(dir, $"*{ext}*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fLower = file.ToLowerInvariant();
                if (fLower.Contains("ios") || fLower.Contains("tvos") ||
                    fLower.Contains("android") || fLower.Contains("browser") ||
                    fLower.Contains("wasm"))
                    continue;

                discovered.Add(file);
            }
        }

        return discovered.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string[] GetPlatformRids()
    {
        var isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return isArm64 ? new[] { "osx-arm64", "osx", "unix" } : new[] { "osx-x64", "osx", "unix" };
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return isArm64 ? new[] { "win-arm64", "win" } : new[] { "win-x64", "win" };
        }
        return isArm64 ? new[] { "linux-arm64", "linux", "unix" } : new[] { "linux-x64", "linux", "unix" };
    }

    private async Task<string?> DownloadAndExtractPackageAsync(
        string packageId,
        string? requestedVersion,
        NuGetResolutionResult result,
        CancellationToken ct)
    {
        var idLower = packageId.ToLowerInvariant();
        var packageDir = Path.Combine(_globalPackagesFolder, idLower);

        try
        {
            string? targetVersion = requestedVersion;

            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                var indexUrl = $"https://api.nuget.org/v3-flatcontainer/{idLower}/index.json";
                result.Messages.Add($"🔍 Querying NuGet for versions of '{packageId}'...");
                var indexJson = await HttpClient.GetStringAsync(indexUrl, ct);

                using var doc = JsonDocument.Parse(indexJson);
                if (doc.RootElement.TryGetProperty("versions", out var versionsElem) &&
                    versionsElem.ValueKind == JsonValueKind.Array)
                {
                    var versions = versionsElem.EnumerateArray()
                        .Select(v => v.GetString())
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Select(v => v!)
                        .ToList();

                    var parsedVersions = versions
                        .Select(v => (Original: v, Version: NuGetSemanticVersion.TryParse(v, out var sem) ? sem : null))
                        .Where(x => x.Version != null)
                        .Select(x => (x.Original, Version: x.Version!))
                        .ToList();

                    var stable = parsedVersions.Where(x => !x.Version.IsPrerelease).OrderByDescending(x => x.Version).ToList();
                    targetVersion = stable.Count > 0 ? stable[0].Original : parsedVersions.OrderByDescending(x => x.Version).FirstOrDefault().Original;
                }
            }

            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                result.Messages.Add($"⚠️ Could not determine suitable version for '{packageId}'.");
                return null;
            }

            var versionLower = targetVersion.ToLowerInvariant();
            var targetDir = Path.Combine(packageDir, targetVersion);
            if (Directory.Exists(targetDir)) return targetDir;

            var nupkgUrl = $"https://api.nuget.org/v3-flatcontainer/{idLower}/{versionLower}/{idLower}.{versionLower}.nupkg";
            result.Messages.Add($"📦 Restoring '{packageId} {targetVersion}' from NuGet.org...");

            var response = await HttpClient.GetAsync(nupkgUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                result.Messages.Add($"⚠️ NuGet restore failed: HTTP {response.StatusCode} for {packageId} {targetVersion}");
                return null;
            }

            var tempNupkg = Path.GetTempFileName();
            try
            {
                await using (var fileStream = File.Create(tempNupkg))
                {
                    await response.Content.CopyToAsync(fileStream, ct);
                }

                Directory.CreateDirectory(targetDir);
                ZipFile.ExtractToDirectory(tempNupkg, targetDir, overwriteFiles: true);
                result.Messages.Add($"✨ Restored '{packageId} {targetVersion}' successfully.");
                return targetDir;
            }
            finally
            {
                if (File.Exists(tempNupkg))
                {
                    try { File.Delete(tempNupkg); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            result.Messages.Add($"⚠️ Could not restore package '{packageId}': {ex.Message}");
            return null;
        }
    }

    private async Task ResolveTransitiveDependenciesAsync(
        string packageDir,
        string packageId,
        NuGetResolutionResult result,
        HashSet<string> visited,
        CancellationToken ct)
    {
        try
        {
            var nuspecFile = Directory.GetFiles(packageDir, "*.nuspec").FirstOrDefault();
            if (nuspecFile == null || !File.Exists(nuspecFile)) return;

            var doc = XDocument.Load(nuspecFile);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var dependenciesNode = doc.Descendants(ns + "dependencies").FirstOrDefault()
                ?? doc.Descendants("dependencies").FirstOrDefault();

            if (dependenciesNode == null) return;

            var groups = dependenciesNode.Elements(ns + "group").ToList();
            if (groups.Count == 0) groups = dependenciesNode.Elements("group").ToList();

            IEnumerable<XElement> depElements;
            if (groups.Count > 0)
            {
                XElement? bestGroup = null;
                foreach (var tfm in PreferredTfms)
                {
                    bestGroup = groups.FirstOrDefault(g =>
                    {
                        var tfmAttr = g.Attribute("targetFramework")?.Value?.Trim() ?? string.Empty;
                        return string.Equals(tfmAttr, tfm, StringComparison.OrdinalIgnoreCase) ||
                               tfmAttr.StartsWith(tfm, StringComparison.OrdinalIgnoreCase);
                    });
                    if (bestGroup != null) break;
                }
                bestGroup ??= groups.FirstOrDefault();
                depElements = bestGroup?.Elements(ns + "dependency") ?? Enumerable.Empty<XElement>();
                if (!depElements.Any())
                {
                    depElements = bestGroup?.Elements("dependency") ?? Enumerable.Empty<XElement>();
                }
            }
            else
            {
                depElements = dependenciesNode.Elements(ns + "dependency");
                if (!depElements.Any())
                {
                    depElements = dependenciesNode.Elements("dependency");
                }
            }

            foreach (var dep in depElements)
            {
                var depId = dep.Attribute("id")?.Value?.Trim();
                var depVer = dep.Attribute("version")?.Value?.Trim();

                if (string.IsNullOrEmpty(depId)) continue;
                if (depId.StartsWith("System.", StringComparison.OrdinalIgnoreCase) && IsCoreRuntimeAssembly(depId))
                    continue;
                if (depId.Equals("NETStandard.Library", StringComparison.OrdinalIgnoreCase))
                    continue;

                await ResolvePackageAsync(depId, depVer, result, visited, ct);
            }
        }
        catch
        {
            // Transitive inspection is non-fatal
        }
    }

    private static bool IsCoreRuntimeAssembly(string assemblyName)
    {
        var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;
        var directDll = Path.Combine(coreDir, assemblyName + ".dll");
        return File.Exists(directDll) ||
               AppDomain.CurrentDomain.GetAssemblies().Any(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
    }

    // Compatibility method for any legacy callers
    public void EnsureSkiaSharpNativeLoaded(string version, NuGetResolutionResult result, Assembly? skiaAssembly = null)
    {
        if (skiaAssembly != null)
        {
            EnsureNativeAssetsResolved(skiaAssembly, result);
        }
    }
}
