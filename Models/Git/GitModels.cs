using System;
using System.Collections.Generic;
using System.IO;

namespace PdfEditorApp.Plugins.CSharpEditor.Models.Git;

public enum GitFileStatusKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted
}

public sealed record GitFileChange(
    string RelativePath,
    GitFileStatusKind StatusKind,
    bool IsStaged,
    string? OldRelativePath = null)
{
    public string FileName => Path.GetFileName(RelativePath);

    public string DirectoryPath
    {
        get
        {
            var dir = Path.GetDirectoryName(RelativePath);
            return string.IsNullOrEmpty(dir) ? string.Empty : dir.Replace('\\', '/');
        }
    }

    public string StatusLetter => StatusKind switch
    {
        GitFileStatusKind.Modified => "M",
        GitFileStatusKind.Added => "A",
        GitFileStatusKind.Deleted => "D",
        GitFileStatusKind.Renamed => "R",
        GitFileStatusKind.Untracked => "U",
        GitFileStatusKind.Conflicted => "!",
        _ => "M"
    };

    public string StatusTooltip => (IsStaged ? "Staged " : string.Empty) + StatusKind switch
    {
        GitFileStatusKind.Modified => "Modified",
        GitFileStatusKind.Added => "Added",
        GitFileStatusKind.Deleted => "Deleted",
        GitFileStatusKind.Renamed => $"Renamed (from {OldRelativePath})",
        GitFileStatusKind.Untracked => "Untracked",
        GitFileStatusKind.Conflicted => "Conflict / Unmerged",
        _ => "Changed"
    };
}

public sealed class GitRepositoryStatus
{
    public bool IsRepository { get; init; }
    public string CurrentBranch { get; init; } = string.Empty;
    public string? UpstreamBranch { get; init; }
    public int AheadCount { get; init; }
    public int BehindCount { get; init; }
    public IReadOnlyList<GitFileChange> StagedChanges { get; init; } = Array.Empty<GitFileChange>();
    public IReadOnlyList<GitFileChange> WorkingChanges { get; init; } = Array.Empty<GitFileChange>();

    public int TotalUncommittedCount => StagedChanges.Count + WorkingChanges.Count;
    public bool HasChanges => TotalUncommittedCount > 0;
    public bool IsClean => IsRepository && !HasChanges;

    public static GitRepositoryStatus NotARepository { get; } = new()
    {
        IsRepository = false,
        CurrentBranch = string.Empty
    };

    public static GitRepositoryStatus EmptyClean(string branch = "main") => new()
    {
        IsRepository = true,
        CurrentBranch = branch
    };
}

public sealed record GitBranchItem(
    string Name,
    bool IsCurrent,
    bool IsRemote,
    string? Upstream = null);

public sealed record GitCommandResult(
    bool Success,
    string Output,
    string? ErrorMessage = null,
    int ExitCode = 0);

public enum GitDiffLineKind
{
    Context,
    Addition,
    Deletion,
    Header,
    Meta
}

public sealed record GitDiffLine(
    GitDiffLineKind Kind,
    string Text,
    int? OldLineNumber = null,
    int? NewLineNumber = null)
{
    public bool IsAddition => Kind == GitDiffLineKind.Addition;
    public bool IsDeletion => Kind == GitDiffLineKind.Deletion;
    public bool IsHeader => Kind == GitDiffLineKind.Header;
    public bool IsContext => Kind == GitDiffLineKind.Context;

    public string Marker => Kind switch
    {
        GitDiffLineKind.Addition => "+",
        GitDiffLineKind.Deletion => "-",
        GitDiffLineKind.Header => "@@",
        _ => " "
    };

    public string OldLineDisplay => OldLineNumber?.ToString() ?? string.Empty;
    public string NewLineDisplay => NewLineNumber?.ToString() ?? string.Empty;
}

public sealed record GitDiffSideBySideRow(
    int? OldLineNumber,
    string? OldText,
    GitDiffLineKind OldKind,
    int? NewLineNumber,
    string? NewText,
    GitDiffLineKind NewKind)
{
    public string OldLineDisplay => OldLineNumber?.ToString() ?? string.Empty;
    public string NewLineDisplay => NewLineNumber?.ToString() ?? string.Empty;

    public bool HasOldContent => OldLineNumber.HasValue || OldKind == GitDiffLineKind.Header;
    public bool HasNewContent => NewLineNumber.HasValue || NewKind == GitDiffLineKind.Header;
}

public sealed class GitDiffDocument
{
    public string RelativePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(RelativePath);
    public bool IsStaged { get; init; }
    public string OriginalContent { get; init; } = string.Empty;
    public string ModifiedContent { get; init; } = string.Empty;
    public IReadOnlyList<GitDiffLine> InlineLines { get; init; } = Array.Empty<GitDiffLine>();
    public IReadOnlyList<GitDiffSideBySideRow> SideBySideRows { get; init; } = Array.Empty<GitDiffSideBySideRow>();
    public int AdditionsCount { get; init; }
    public int DeletionsCount { get; init; }
    public bool HasChanges => AdditionsCount > 0 || DeletionsCount > 0 || InlineLines.Count > 0;
}
