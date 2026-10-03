using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models.Git;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace.Git;

public static class GitCommitMessageGenerator
{
    public static string Generate(IReadOnlyList<GitFileChange> changes, string? diffContent = null)
    {
        if (changes == null || changes.Count == 0)
        {
            return "chore: update workspace files";
        }

        // 1. Determine Scope
        string scope = DetermineScope(changes);

        // 2. Determine Commit Type
        string type = DetermineType(changes, diffContent);

        // 3. Determine Subject Description
        // 3. Determine Subject Description
        string subject = DetermineSubject(changes, scope);

        if (string.Equals(type, scope, StringComparison.OrdinalIgnoreCase))
        {
            scope = string.Empty;
        }

        return string.IsNullOrEmpty(scope)
            ? $"{type}: {subject}"
            : $"{type}({scope}): {subject}";
    }

    private static string DetermineType(IReadOnlyList<GitFileChange> changes, string? diffContent)
    {
        // If all files are tests
        if (changes.All(c => c.RelativePath.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                             c.RelativePath.EndsWith(".Tests.cs", StringComparison.OrdinalIgnoreCase)))
        {
            return "test";
        }

        // If all files are markdown or doc files
        if (changes.All(c => c.RelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                             c.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                             c.RelativePath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)))
        {
            return "docs";
        }

        // If files indicate bug fixing
        if (diffContent != null && (diffContent.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
                                    diffContent.Contains("bug", StringComparison.OrdinalIgnoreCase) ||
                                    diffContent.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
                                    diffContent.Contains("regression", StringComparison.OrdinalIgnoreCase)))
        {
            return "fix";
        }

        // If files indicate refactor
        if (changes.Any(c => c.StatusKind == GitFileStatusKind.Renamed))
        {
            return "refactor";
        }

        return "feat";
    }

    private static string DetermineScope(IReadOnlyList<GitFileChange> changes)
    {
        var dirs = changes
            .Select(c => Path.GetDirectoryName(c.RelativePath)?.Replace('\\', '/'))
            .Where(d => !string.IsNullOrEmpty(d))
            .ToList();

        if (dirs.Count == 0)
        {
            // Root file changed
            var first = changes[0].FileName;
            var ext = Path.GetExtension(first).TrimStart('.').ToLowerInvariant();
            return !string.IsNullOrEmpty(ext) ? ext : string.Empty;
        }

        // Check prominent known domains
        if (dirs.Any(d => d!.Contains("Git", StringComparison.OrdinalIgnoreCase))) return "git";
        if (dirs.Any(d => d!.Contains("Roslyn", StringComparison.OrdinalIgnoreCase))) return "roslyn";
        if (dirs.Any(d => d!.Contains("Controls", StringComparison.OrdinalIgnoreCase) || d!.Contains("Views", StringComparison.OrdinalIgnoreCase))) return "ui";
        if (dirs.Any(d => d!.Contains("Languages", StringComparison.OrdinalIgnoreCase))) return "languages";
        if (dirs.Any(d => d!.Contains("Notebooks", StringComparison.OrdinalIgnoreCase))) return "notebooks";
        if (dirs.Any(d => d!.Contains("Explorer", StringComparison.OrdinalIgnoreCase))) return "explorer";
        if (dirs.Any(d => d!.Contains("Tests", StringComparison.OrdinalIgnoreCase))) return "test";

        // Extract highest common folder
        var segments = dirs[0]!.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1].ToLowerInvariant() : string.Empty;
    }

    private static string DetermineSubject(IReadOnlyList<GitFileChange> changes, string scope)
    {
        if (changes.Count == 1)
        {
            var single = changes[0];
            return single.StatusKind switch
            {
                GitFileStatusKind.Added or GitFileStatusKind.Untracked => $"add {single.FileName}",
                GitFileStatusKind.Deleted => $"remove {single.FileName}",
                GitFileStatusKind.Renamed => $"rename {single.OldRelativePath} to {single.FileName}",
                _ => $"update {single.FileName}"
            };
        }

        int addCount = changes.Count(c => c.StatusKind == GitFileStatusKind.Added || c.StatusKind == GitFileStatusKind.Untracked);
        int modCount = changes.Count(c => c.StatusKind == GitFileStatusKind.Modified);
        int delCount = changes.Count(c => c.StatusKind == GitFileStatusKind.Deleted);

        if (addCount > 0 && modCount == 0 && delCount == 0)
        {
            return $"add {addCount} files in {scope}";
        }
        if (delCount > 0 && modCount == 0 && addCount == 0)
        {
            return $"remove {delCount} files from {scope}";
        }

        return $"update {scope} implementation and components";
    }
}
