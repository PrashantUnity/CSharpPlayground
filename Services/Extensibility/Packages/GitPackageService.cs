using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Packages;

public sealed class GitPackageResolutionResult
{
    public bool Success { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string ExtensionDirectory { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public ExtensionManifest? Manifest { get; set; }
}

/// <summary>
/// Service responsible for resolving, cloning, downloading, caching, and locking
/// Git-based extensions matching Unity Package Manager semantics.
/// </summary>
public sealed class GitPackageService
{
    private readonly IHostEnvironment _host;
    private readonly HttpClient _httpClient;
    private readonly string _globalCacheRoot;

    public GitPackageService(
        IHostEnvironment? host = null,
        HttpClient? httpClient = null,
        string? customCacheRoot = null)
    {
        _host = host ?? new HostEnvironment();
        _httpClient = httpClient ?? new HttpClient();
        _globalCacheRoot = customCacheRoot ?? Path.Combine(_host.HomeDirectory, ".frysharp", "cache", "packages", "git");
    }

    public string GlobalCacheRoot => _globalCacheRoot;

    /// <summary>
    /// Resolves and acquires a package from a Git URL using Git CLI or GitHub archive fallback.
    /// </summary>
    public async Task<GitPackageResolutionResult> ResolveAndDownloadAsync(
        GitPackageUrl url,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        string refOrRev = string.IsNullOrWhiteSpace(url.Revision) ? "HEAD" : url.Revision;
        string safeRevFolder = SanitizePath(refOrRev);
        string packageCacheDir = Path.Combine(_globalCacheRoot, url.CacheKey, safeRevFolder);

        string targetExtensionDir = string.IsNullOrEmpty(url.SubPath)
            ? packageCacheDir
            : Path.Combine(packageCacheDir, url.SubPath);

        // 1. Check if already cached and valid
        if (!forceRefresh && Directory.Exists(targetExtensionDir))
        {
            string manifestPath = Path.Combine(targetExtensionDir, "extension.json");
            if (File.Exists(manifestPath))
            {
                var manifest = await TryReadManifestAsync(manifestPath, ct);
                if (manifest != null)
                {
                    return new GitPackageResolutionResult
                    {
                        Success = true,
                        PackageId = manifest.Id,
                        ExtensionDirectory = targetExtensionDir,
                        CommitSha = refOrRev,
                        Manifest = manifest
                    };
                }
            }
        }

        // 2. Attempt acquisition via Git CLI
        bool gitSuccess = false;
        string commitSha = refOrRev;

        if (await IsGitCliAvailableAsync(ct))
        {
            var gitResult = await CloneViaGitCliAsync(url, packageCacheDir, refOrRev, ct);
            if (gitResult.Success)
            {
                gitSuccess = true;
                commitSha = gitResult.CommitSha;
            }
        }

        // 3. Fallback to GitHub Archive Download if Git CLI unavailable or failed
        if (!gitSuccess && url.IsGitHub && !string.IsNullOrEmpty(url.Owner))
        {
            var httpResult = await DownloadGitHubArchiveAsync(url, packageCacheDir, refOrRev, ct);
            if (httpResult.Success)
            {
                gitSuccess = true;
                commitSha = httpResult.CommitSha;
            }
            else if (!gitSuccess)
            {
                return new GitPackageResolutionResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to acquire package via Git and Archive fallback: {httpResult.ErrorMessage}"
                };
            }
        }

        if (!gitSuccess)
        {
            return new GitPackageResolutionResult
            {
                Success = false,
                ErrorMessage = $"Unable to clone or download repository '{url.RepositoryUrl}'. Ensure git is installed on PATH or the repository is publicly accessible."
            };
        }

        // 4. Validate extension.json in target folder
        string finalManifestPath = Path.Combine(targetExtensionDir, "extension.json");
        if (!File.Exists(finalManifestPath))
        {
            return new GitPackageResolutionResult
            {
                Success = false,
                ErrorMessage = $"Package downloaded but extension.json was not found at '{targetExtensionDir}'."
            };
        }

        var loadedManifest = await TryReadManifestAsync(finalManifestPath, ct);
        if (loadedManifest == null)
        {
            return new GitPackageResolutionResult
            {
                Success = false,
                ErrorMessage = $"Failed to parse extension.json in package '{targetExtensionDir}'."
            };
        }

        return new GitPackageResolutionResult
        {
            Success = true,
            PackageId = loadedManifest.Id,
            ExtensionDirectory = targetExtensionDir,
            CommitSha = commitSha,
            Manifest = loadedManifest
        };
    }

    /// <summary>
    /// Restores all packages declared in workspace .frysharp/extensions.json and updates lockfile.
    /// </summary>
    public async Task<IReadOnlyList<GitPackageResolutionResult>> RestoreWorkspacePackagesAsync(
        string workspaceRoot,
        CancellationToken ct = default)
    {
        var results = new List<GitPackageResolutionResult>();
        string manifestPath = Path.Combine(workspaceRoot, ".frysharp", "extensions.json");
        string lockfilePath = Path.Combine(workspaceRoot, ".frysharp", "extensions-lock.json");

        if (!File.Exists(manifestPath))
        {
            return results;
        }

        var manifest = await WorkspaceExtensionManifest.LoadAsync(manifestPath, ct);
        var lockfile = await WorkspaceExtensionLockfile.LoadAsync(lockfilePath, ct);

        foreach (var (pkgId, urlStr) in manifest.Dependencies)
        {
            if (GitPackageUrl.TryParse(urlStr, out var url) && url != null)
            {
                var res = await ResolveAndDownloadAsync(url, forceRefresh: false, ct);
                results.Add(res);

                if (res.Success && res.Manifest != null)
                {
                    lockfile.Dependencies[pkgId] = new LockedGitPackage
                    {
                        PackageId = res.Manifest.Id,
                        Url = url.RepositoryUrl,
                        ResolvedRef = url.Revision,
                        CommitSha = res.CommitSha,
                        SubPath = url.SubPath,
                        CacheDirectory = res.ExtensionDirectory,
                        Version = res.Manifest.Version,
                        InstalledAtUtc = DateTimeOffset.UtcNow
                    };
                }
            }
        }

        await lockfile.SaveAsync(lockfilePath, ct);
        return results;
    }

    /// <summary>
    /// Adds a Git package dependency to the workspace and locks it.
    /// </summary>
    public async Task<GitPackageResolutionResult> InstallPackageToWorkspaceAsync(
        string workspaceRoot,
        string gitUrlString,
        CancellationToken ct = default)
    {
        if (!GitPackageUrl.TryParse(gitUrlString, out var url) || url == null)
        {
            return new GitPackageResolutionResult
            {
                Success = false,
                ErrorMessage = $"Invalid Git package URL: '{gitUrlString}'"
            };
        }

        var res = await ResolveAndDownloadAsync(url, forceRefresh: false, ct);
        if (!res.Success || res.Manifest == null)
        {
            return res;
        }

        string manifestPath = Path.Combine(workspaceRoot, ".frysharp", "extensions.json");
        string lockfilePath = Path.Combine(workspaceRoot, ".frysharp", "extensions-lock.json");

        var manifest = await WorkspaceExtensionManifest.LoadAsync(manifestPath, ct);
        var lockfile = await WorkspaceExtensionLockfile.LoadAsync(lockfilePath, ct);

        manifest.Dependencies[res.Manifest.Id] = gitUrlString;
        await manifest.SaveAsync(manifestPath, ct);

        lockfile.Dependencies[res.Manifest.Id] = new LockedGitPackage
        {
            PackageId = res.Manifest.Id,
            Url = url.RepositoryUrl,
            ResolvedRef = url.Revision,
            CommitSha = res.CommitSha,
            SubPath = url.SubPath,
            CacheDirectory = res.ExtensionDirectory,
            Version = res.Manifest.Version,
            InstalledAtUtc = DateTimeOffset.UtcNow
        };
        await lockfile.SaveAsync(lockfilePath, ct);

        return res;
    }

    /// <summary>
    /// Removes a package dependency from workspace manifest and lockfile.
    /// </summary>
    public async Task<bool> UninstallPackageFromWorkspaceAsync(
        string workspaceRoot,
        string packageId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(packageId)) return false;

        string manifestPath = Path.Combine(workspaceRoot, ".frysharp", "extensions.json");
        string lockfilePath = Path.Combine(workspaceRoot, ".frysharp", "extensions-lock.json");

        bool modified = false;
        if (File.Exists(manifestPath))
        {
            var manifest = await WorkspaceExtensionManifest.LoadAsync(manifestPath, ct);
            if (manifest.Dependencies.Remove(packageId))
            {
                await manifest.SaveAsync(manifestPath, ct);
                modified = true;
            }
        }

        if (File.Exists(lockfilePath))
        {
            var lockfile = await WorkspaceExtensionLockfile.LoadAsync(lockfilePath, ct);
            if (lockfile.Dependencies.Remove(packageId))
            {
                await lockfile.SaveAsync(lockfilePath, ct);
                modified = true;
            }
        }

        return modified;
    }

    private async Task<bool> IsGitCliAvailableAsync(CancellationToken ct)
    {
        var res = await _host.RunAsync("git", ["--version"], TimeSpan.FromSeconds(3), ct);
        return res.ExitCode == 0;
    }

    private async Task<(bool Success, string CommitSha, string? ErrorMessage)> CloneViaGitCliAsync(
        GitPackageUrl url,
        string targetDir,
        string refOrRev,
        CancellationToken ct)
    {
        try
        {
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, recursive: true);
            }
            Directory.CreateDirectory(targetDir);

            // Attempt shallow clone with branch/tag
            var cloneArgs = !string.IsNullOrEmpty(url.Revision) && url.Revision != "HEAD"
                ? new[] { "clone", "--depth", "1", "--branch", url.Revision, url.RepositoryUrl, targetDir }
                : new[] { "clone", "--depth", "1", url.RepositoryUrl, targetDir };

            var cloneRes = await _host.RunAsync("git", cloneArgs, TimeSpan.FromSeconds(60), ct);

            // If clone by branch failed (e.g. revision is a commit SHA), try full clone + checkout
            if (cloneRes.ExitCode != 0 && !string.IsNullOrEmpty(url.Revision))
            {
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, recursive: true);
                Directory.CreateDirectory(targetDir);

                var fallbackClone = await _host.RunAsync("git", ["clone", url.RepositoryUrl, targetDir], TimeSpan.FromSeconds(90), ct);
                if (fallbackClone.ExitCode != 0)
                {
                    return (false, refOrRev, fallbackClone.StandardError);
                }

                var checkoutRes = await _host.RunAsync("git", ["-C", targetDir, "checkout", url.Revision], TimeSpan.FromSeconds(30), ct);
                if (checkoutRes.ExitCode != 0)
                {
                    return (false, refOrRev, checkoutRes.StandardError);
                }
            }

            // Read exact resolved SHA
            var shaRes = await _host.RunAsync("git", ["-C", targetDir, "rev-parse", "HEAD"], TimeSpan.FromSeconds(5), ct);
            string sha = shaRes.ExitCode == 0 && !string.IsNullOrWhiteSpace(shaRes.StandardOutput)
                ? shaRes.StandardOutput.Trim()
                : refOrRev;

            return (true, sha, null);
        }
        catch (Exception ex)
        {
            return (false, refOrRev, ex.Message);
        }
    }

    private async Task<(bool Success, string CommitSha, string? ErrorMessage)> DownloadGitHubArchiveAsync(
        GitPackageUrl url,
        string targetDir,
        string refOrRev,
        CancellationToken ct)
    {
        try
        {
            string targetBranchOrRef = string.IsNullOrEmpty(url.Revision) || url.Revision == "HEAD" ? "main" : url.Revision;
            string zipUrl = $"https://github.com/{url.Owner}/{url.RepositoryName}/archive/{targetBranchOrRef}.zip";

            using var req = new HttpRequestMessage(HttpMethod.Get, zipUrl);
            req.Headers.Add("User-Agent", "FrySharp-PackageManager");

            var response = await _httpClient.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode && targetBranchOrRef == "main")
            {
                // Try fallback to master
                string masterZipUrl = $"https://github.com/{url.Owner}/{url.RepositoryName}/archive/master.zip";
                using var reqMaster = new HttpRequestMessage(HttpMethod.Get, masterZipUrl);
                reqMaster.Headers.Add("User-Agent", "FrySharp-PackageManager");
                response = await _httpClient.SendAsync(reqMaster, ct);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (false, targetBranchOrRef, $"HTTP {response.StatusCode} when fetching {zipUrl}");
            }

            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, recursive: true);
            }
            Directory.CreateDirectory(targetDir);

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            // GitHub archives wrap contents in a root directory (e.g. "repo-ref/")
            string? rootPrefix = archive.Entries.FirstOrDefault()?.FullName;
            int firstSlash = rootPrefix?.IndexOf('/') ?? -1;
            string prefixToStrip = firstSlash > 0 ? rootPrefix![..(firstSlash + 1)] : string.Empty;

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // Directory entry

                string relativePath = entry.FullName;
                if (!string.IsNullOrEmpty(prefixToStrip) && relativePath.StartsWith(prefixToStrip, StringComparison.Ordinal))
                {
                    relativePath = relativePath[prefixToStrip.Length..];
                }

                string outPath = Path.Combine(targetDir, relativePath);
                string? outDir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                entry.ExtractToFile(outPath, overwrite: true);
            }

            return (true, targetBranchOrRef, null);
        }
        catch (Exception ex)
        {
            return (false, refOrRev, ex.Message);
        }
    }

    private static async Task<ExtensionManifest?> TryReadManifestAsync(string path, CancellationToken ct)
    {
        try
        {
            string json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<ExtensionManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\' || chars[i] == ':')
            {
                chars[i] = '_';
            }
        }
        return new string(chars);
    }
}
