using System;
using System.IO;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Packages;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class GitPackageImportingTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FrySharp_GitPkgTests_" + Guid.NewGuid().ToString("N"));

    public GitPackageImportingTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Theory]
    [InlineData("https://github.com/CodeFryDev/FrySharp.Zig.git", "https://github.com/CodeFryDev/FrySharp.Zig.git", null, "")]
    [InlineData("https://github.com/CodeFryDev/FrySharp.Zig", "https://github.com/CodeFryDev/FrySharp.Zig.git", null, "")]
    [InlineData("https://github.com/CodeFryDev/FrySharp.Zig.git#v1.0.0", "https://github.com/CodeFryDev/FrySharp.Zig.git", "v1.0.0", "")]
    [InlineData("https://github.com/CodeFryDev/FrySharp.Zig.git#main", "https://github.com/CodeFryDev/FrySharp.Zig.git", "main", "")]
    [InlineData("https://github.com/CodeFryDev/FrySharp.Zig.git#a1b2c3d4", "https://github.com/CodeFryDev/FrySharp.Zig.git", "a1b2c3d4", "")]
    [InlineData("https://github.com/CodeFryDev/FrySharp.Zig.git?path=/sub/pkg#v2.1.0", "https://github.com/CodeFryDev/FrySharp.Zig.git", "v2.1.0", "sub/pkg")]
    [InlineData("https://github.com/CodeFryDev/FrySharp.Zig.git#v2.1.0?path=/sub/pkg", "https://github.com/CodeFryDev/FrySharp.Zig.git", "v2.1.0", "sub/pkg")]
    [InlineData("git@github.com:CodeFryDev/FrySharp.Zig.git#v1.0.0", "git@github.com:CodeFryDev/FrySharp.Zig.git", "v1.0.0", "")]
    [InlineData("github:CodeFryDev/FrySharp.Zig#v1.0.0", "https://github.com/CodeFryDev/FrySharp.Zig.git", "v1.0.0", "")]
    [InlineData("CodeFryDev/FrySharp.Zig#v1.0.0", "https://github.com/CodeFryDev/FrySharp.Zig.git", "v1.0.0", "")]
    public void GitPackageUrl_ParsesSupportedFormats(string input, string expectedRepo, string? expectedRef, string expectedSubPath)
    {
        bool success = GitPackageUrl.TryParse(input, out var result);

        Assert.True(success, $"Failed to parse: {input}");
        Assert.NotNull(result);
        Assert.Equal(expectedRepo, result.RepositoryUrl);
        Assert.Equal(expectedRef, result.Revision);
        Assert.Equal(expectedSubPath, result.SubPath);
        Assert.NotEmpty(result.CacheKey);
        Assert.True(result.IsGitHub);
    }

    [Fact]
    public void GitPackageUrl_RejectsInvalidInput()
    {
        Assert.False(GitPackageUrl.TryParse("", out _));
        Assert.False(GitPackageUrl.TryParse("   ", out _));
        Assert.False(GitPackageUrl.TryParse("not_a_url_or_shorthand", out _));
        Assert.Throws<FormatException>(() => GitPackageUrl.Parse("bad_format:::"));
    }

    [Fact]
    public void GitPackageUrl_GeneratesConsistentCacheKey()
    {
        var url1 = GitPackageUrl.Parse("https://github.com/CodeFryDev/FrySharp.Zig.git#v1.0.0");
        var url2 = GitPackageUrl.Parse("https://github.com/CodeFryDev/FrySharp.Zig.git#v2.0.0");

        Assert.Equal(url1.CacheKey, url2.CacheKey);
        Assert.Contains("codefrydev", url1.CacheKey);
        Assert.Contains("frysharp.zig", url1.CacheKey);
    }

    [Fact]
    public async Task WorkspaceExtensionManifest_SavesAndLoadsRoundTrip()
    {
        string manifestPath = Path.Combine(_tempDir, ".frysharp", "extensions.json");

        var manifest = new WorkspaceExtensionManifest();
        manifest.Dependencies["com.frysharp.zig"] = "https://github.com/CodeFryDev/FrySharp.Zig.git#v1.0.0";
        manifest.Dependencies["com.frysharp.dart"] = "https://github.com/CodeFryDev/FrySharp.Dart.git#v1.2.0";

        await manifest.SaveAsync(manifestPath);
        Assert.True(File.Exists(manifestPath));

        var loaded = await WorkspaceExtensionManifest.LoadAsync(manifestPath);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Dependencies.Count);
        Assert.Equal("https://github.com/CodeFryDev/FrySharp.Zig.git#v1.0.0", loaded.Dependencies["com.frysharp.zig"]);
        Assert.Equal("https://github.com/CodeFryDev/FrySharp.Dart.git#v1.2.0", loaded.Dependencies["com.frysharp.dart"]);
    }

    [Fact]
    public async Task WorkspaceExtensionLockfile_SavesAndLoadsRoundTrip()
    {
        string lockfilePath = Path.Combine(_tempDir, ".frysharp", "extensions-lock.json");

        var lockfile = new WorkspaceExtensionLockfile();
        lockfile.Dependencies["com.frysharp.zig"] = new LockedGitPackage
        {
            PackageId = "com.frysharp.zig",
            Url = "https://github.com/CodeFryDev/FrySharp.Zig.git",
            ResolvedRef = "v1.0.0",
            CommitSha = "a1b2c3d4e5f67890abcdef1234567890abcdef12",
            CacheDirectory = "/cache/packages/zig",
            Version = "1.0.0"
        };

        await lockfile.SaveAsync(lockfilePath);
        Assert.True(File.Exists(lockfilePath));

        var loaded = await WorkspaceExtensionLockfile.LoadAsync(lockfilePath);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Dependencies);
        var pkg = loaded.Dependencies["com.frysharp.zig"];
        Assert.Equal("a1b2c3d4e5f67890abcdef1234567890abcdef12", pkg.CommitSha);
        Assert.Equal("/cache/packages/zig", pkg.CacheDirectory);
    }

    [Fact]
    public async Task ExtensionManager_DiscoversLockedPackageDirectories()
    {
        string workspaceRoot = Path.Combine(_tempDir, "my-workspace");
        Directory.CreateDirectory(workspaceRoot);

        string mockCacheDir = Path.Combine(_tempDir, "cache", "pkg-zig");
        Directory.CreateDirectory(mockCacheDir);

        string lockfilePath = Path.Combine(workspaceRoot, ".frysharp", "extensions-lock.json");
        var lockfile = new WorkspaceExtensionLockfile();
        lockfile.Dependencies["com.frysharp.zig"] = new LockedGitPackage
        {
            PackageId = "com.frysharp.zig",
            CacheDirectory = mockCacheDir,
            CommitSha = "123456"
        };
        await lockfile.SaveAsync(lockfilePath);

        var dirs = ExtensionManager.GetDefaultExtensionSearchDirectories(workspaceRoot);
        Assert.Contains(dirs, d => string.Equals(d, mockCacheDir, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GitPackageService_ResolvesPreCachedPackage_WithoutNetwork()
    {
        string cacheRoot = Path.Combine(_tempDir, "global-cache");
        var pkgService = new GitPackageService(customCacheRoot: cacheRoot);

        var url = GitPackageUrl.Parse("https://github.com/CodeFryDev/FrySharp.Zig.git#v1.0.0");
        string expectedDir = Path.Combine(cacheRoot, url.CacheKey, "v1.0.0");
        Directory.CreateDirectory(expectedDir);

        string manifestJson = """
            {
              "id": "frysharp.zig",
              "name": "Zig Language Support",
              "version": "1.0.0"
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(expectedDir, "extension.json"), manifestJson);

        var result = await pkgService.ResolveAndDownloadAsync(url);

        Assert.True(result.Success);
        Assert.Equal("frysharp.zig", result.PackageId);
        Assert.Equal(expectedDir, result.ExtensionDirectory);
        Assert.NotNull(result.Manifest);
        Assert.Equal("Zig Language Support", result.Manifest.Name);
    }
}
