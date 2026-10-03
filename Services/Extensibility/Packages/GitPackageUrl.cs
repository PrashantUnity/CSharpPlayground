using System;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Packages;

/// <summary>
/// Parses and normalizes Git package URLs matching Unity Package Manager (UPM) syntax:
/// e.g. "https://github.com/owner/repo.git#v1.0.0?path=/subfolder"
/// Supports HTTPS, SSH, GitHub shorthand ("owner/repo#v1.0.0"), query subpaths, and revisions.
/// </summary>
public sealed record GitPackageUrl
{
    private static readonly Regex GitHubShorthandRegex = new(
        @"^(?:github:)?([a-zA-Z0-9_\-\.]+)/([a-zA-Z0-9_\-\.]+)(?:#([^?]+))?(?:\?path=(.+))?$",
        RegexOptions.Compiled);

    private static readonly Regex SshGitRegex = new(
        @"^git@([a-zA-Z0-9_\-\.]+):([a-zA-Z0-9_\-\.]+)/([a-zA-Z0-9_\-\.]+?)(?:\.git)?(?:#([^?]+))?(?:\?path=(.+))?$",
        RegexOptions.Compiled);

    public string OriginalString { get; init; } = string.Empty;
    public string RepositoryUrl { get; init; } = string.Empty;
    public string? Revision { get; init; }
    public string SubPath { get; init; } = string.Empty;
    public string? Owner { get; init; }
    public string RepositoryName { get; init; } = string.Empty;
    public bool IsGitHub { get; init; }

    /// <summary>
    /// File-system friendly cache key for this repository (e.g. "github.com-owner-repo").
    /// </summary>
    public string CacheKey
    {
        get
        {
            string host = "git";
            try
            {
                if (Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri))
                {
                    host = uri.Host;
                }
            }
            catch { }

            string ownerPart = string.IsNullOrEmpty(Owner) ? "" : $"{Owner}-";
            string cleanRepo = RepositoryName.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? RepositoryName[..^4]
                : RepositoryName;

            string key = $"{host}-{ownerPart}{cleanRepo}".ToLowerInvariant();
            return SanitizeFileName(key);
        }
    }

    public static bool TryParse(string input, out GitPackageUrl? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string trimmed = input.Trim();

        // 1. Check GitHub shorthand ("owner/repo#v1.0.0" or "github:owner/repo#v1.0.0")
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            var shortMatch = GitHubShorthandRegex.Match(trimmed);
            if (shortMatch.Success && !trimmed.Contains(':') || trimmed.StartsWith("github:", StringComparison.OrdinalIgnoreCase))
            {
                string owner = shortMatch.Groups[1].Value;
                string repo = shortMatch.Groups[2].Value;
                string? rev = shortMatch.Groups[3].Success ? shortMatch.Groups[3].Value : null;
                string subPath = shortMatch.Groups[4].Success ? shortMatch.Groups[4].Value : string.Empty;

                result = new GitPackageUrl
                {
                    OriginalString = trimmed,
                    RepositoryUrl = $"https://github.com/{owner}/{repo}.git",
                    Revision = rev,
                    SubPath = NormalizeSubPath(subPath),
                    Owner = owner,
                    RepositoryName = repo,
                    IsGitHub = true
                };
                return true;
            }
        }

        // 2. Check SSH format ("git@github.com:owner/repo.git#ref?path=/sub")
        if (trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            var sshMatch = SshGitRegex.Match(trimmed);
            if (sshMatch.Success)
            {
                string host = sshMatch.Groups[1].Value;
                string owner = sshMatch.Groups[2].Value;
                string repo = sshMatch.Groups[3].Value;
                string? rev = sshMatch.Groups[4].Success ? sshMatch.Groups[4].Value : null;
                string subPath = sshMatch.Groups[5].Success ? sshMatch.Groups[5].Value : string.Empty;

                result = new GitPackageUrl
                {
                    OriginalString = trimmed,
                    RepositoryUrl = $"git@{host}:{owner}/{repo}.git",
                    Revision = rev,
                    SubPath = NormalizeSubPath(subPath),
                    Owner = owner,
                    RepositoryName = repo,
                    IsGitHub = host.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                };
                return true;
            }
        }

        // 3. Standard HTTP / HTTPS URL
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string workUrl = trimmed;
                string? rev = null;
                string subPath = string.Empty;

                // Unity supports fragment #revision
                int hashIdx = workUrl.IndexOf('#');
                if (hashIdx >= 0)
                {
                    string afterHash = workUrl[(hashIdx + 1)..];
                    workUrl = workUrl[..hashIdx];

                    // Check if query params were put after the hash (e.g. #v1.0.0?path=/sub)
                    int qIdx = afterHash.IndexOf("?path=", StringComparison.OrdinalIgnoreCase);
                    if (qIdx >= 0)
                    {
                        subPath = afterHash[(qIdx + 6)..];
                        rev = afterHash[..qIdx];
                    }
                    else
                    {
                        rev = afterHash;
                    }
                }

                // Check for ?path= in main url if not found after hash
                int pathQueryIdx = workUrl.IndexOf("?path=", StringComparison.OrdinalIgnoreCase);
                if (pathQueryIdx >= 0)
                {
                    if (string.IsNullOrEmpty(subPath))
                    {
                        subPath = workUrl[(pathQueryIdx + 6)..];
                        int ampIdx = subPath.IndexOf('&');
                        if (ampIdx >= 0) subPath = subPath[..ampIdx];
                    }
                    workUrl = workUrl[..pathQueryIdx];
                }

                if (!Uri.TryCreate(workUrl, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                string segs = uri.AbsolutePath.Trim('/');
                string[] parts = segs.Split('/', StringSplitOptions.RemoveEmptyEntries);
                string owner = parts.Length >= 2 ? parts[^2] : string.Empty;
                string repo = parts.Length >= 1 ? parts[^1] : string.Empty;

                if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    repo = repo[..^4];
                }

                // Ensure repository URL ends with .git for clean git clone
                string normalizedRepoUrl = uri.GetLeftPart(UriPartial.Path);
                if (!normalizedRepoUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedRepoUrl += ".git";
                }

                result = new GitPackageUrl
                {
                    OriginalString = trimmed,
                    RepositoryUrl = normalizedRepoUrl,
                    Revision = string.IsNullOrWhiteSpace(rev) ? null : rev.Trim(),
                    SubPath = NormalizeSubPath(subPath),
                    Owner = string.IsNullOrEmpty(owner) ? null : owner,
                    RepositoryName = repo,
                    IsGitHub = uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public static GitPackageUrl Parse(string input)
    {
        if (!TryParse(input, out var res) || res == null)
        {
            throw new FormatException($"Invalid Git package URL format: '{input}'");
        }
        return res;
    }

    private static string NormalizeSubPath(string subPath)
    {
        if (string.IsNullOrWhiteSpace(subPath)) return string.Empty;
        string cleaned = subPath.Trim().Replace('\\', '/');
        if (cleaned.StartsWith('/')) cleaned = cleaned[1..];
        if (cleaned.EndsWith('/')) cleaned = cleaned[..^1];
        return cleaned;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\' || chars[i] == ':')
            {
                chars[i] = '-';
            }
        }
        return new string(chars);
    }
}
