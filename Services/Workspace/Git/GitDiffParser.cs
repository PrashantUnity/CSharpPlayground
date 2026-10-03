using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models.Git;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace.Git;

public static partial class GitDiffParser
{
    [GeneratedRegex(@"^@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@(.*)$")]
    private static partial Regex ChunkHeaderRegex();

    public static GitDiffDocument Parse(
        string relativePath,
        bool isStaged,
        string? diffOutput,
        string? originalContent,
        string? modifiedContent)
    {
        var inline = new List<GitDiffLine>();
        var sideBySide = new List<GitDiffSideBySideRow>();
        int additions = 0;
        int deletions = 0;

        if (!string.IsNullOrWhiteSpace(diffOutput))
        {
            ParseUnifiedDiff(diffOutput, inline, sideBySide, ref additions, ref deletions);
        }
        else if (!string.IsNullOrEmpty(modifiedContent) && string.IsNullOrEmpty(originalContent))
        {
            // Untracked / newly created file
            SynthesizeFullAddition(modifiedContent, inline, sideBySide, ref additions);
        }
        else if (!string.IsNullOrEmpty(originalContent) && string.IsNullOrEmpty(modifiedContent))
        {
            // Deleted file
            SynthesizeFullDeletion(originalContent, inline, sideBySide, ref deletions);
        }
        else if (originalContent != null && modifiedContent != null && originalContent != modifiedContent)
        {
            // Fallback simple line diff
            SynthesizeSimpleDiff(originalContent, modifiedContent, inline, sideBySide, ref additions, ref deletions);
        }

        return new GitDiffDocument
        {
            RelativePath = relativePath,
            IsStaged = isStaged,
            OriginalContent = originalContent ?? string.Empty,
            ModifiedContent = modifiedContent ?? string.Empty,
            InlineLines = inline,
            SideBySideRows = sideBySide,
            AdditionsCount = additions,
            DeletionsCount = deletions
        };
    }

    private static void ParseUnifiedDiff(
        string diffOutput,
        List<GitDiffLine> inline,
        List<GitDiffSideBySideRow> sideBySide,
        ref int additions,
        ref int deletions)
    {
        var rawLines = diffOutput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int oldLine = 1;
        int newLine = 1;
        bool inHunk = false;

        var pendingDeletions = new List<(int lineNum, string text)>();
        var pendingAdditions = new List<(int lineNum, string text)>();

        void FlushHunkPairs()
        {
            int max = Math.Max(pendingDeletions.Count, pendingAdditions.Count);
            for (int i = 0; i < max; i++)
            {
                int? oNum = i < pendingDeletions.Count ? pendingDeletions[i].lineNum : null;
                string? oText = i < pendingDeletions.Count ? pendingDeletions[i].text : null;
                var oKind = oNum.HasValue ? GitDiffLineKind.Deletion : GitDiffLineKind.Context;

                int? nNum = i < pendingAdditions.Count ? pendingAdditions[i].lineNum : null;
                string? nText = i < pendingAdditions.Count ? pendingAdditions[i].text : null;
                var nKind = nNum.HasValue ? GitDiffLineKind.Addition : GitDiffLineKind.Context;

                sideBySide.Add(new GitDiffSideBySideRow(oNum, oText, oKind, nNum, nText, nKind));
            }
            pendingDeletions.Clear();
            pendingAdditions.Clear();
        }

        foreach (var rawLine in rawLines)
        {
            if (rawLine.StartsWith("diff --git") || rawLine.StartsWith("index ") ||
                rawLine.StartsWith("--- ") || rawLine.StartsWith("+++ "))
            {
                FlushHunkPairs();
                inHunk = false;
                continue;
            }

            var chunkMatch = ChunkHeaderRegex().Match(rawLine);
            if (chunkMatch.Success)
            {
                FlushHunkPairs();
                inHunk = true;
                oldLine = int.Parse(chunkMatch.Groups[1].Value);
                newLine = int.Parse(chunkMatch.Groups[3].Value);

                inline.Add(new GitDiffLine(GitDiffLineKind.Header, rawLine));
                sideBySide.Add(new GitDiffSideBySideRow(null, rawLine, GitDiffLineKind.Header, null, rawLine, GitDiffLineKind.Header));
                continue;
            }

            if (!inHunk) continue;

            if (rawLine.StartsWith('+'))
            {
                additions++;
                string content = rawLine[1..];
                inline.Add(new GitDiffLine(GitDiffLineKind.Addition, content, null, newLine));
                pendingAdditions.Add((newLine, content));
                newLine++;
            }
            else if (rawLine.StartsWith('-'))
            {
                deletions++;
                string content = rawLine[1..];
                inline.Add(new GitDiffLine(GitDiffLineKind.Deletion, content, oldLine, null));
                pendingDeletions.Add((oldLine, content));
                oldLine++;
            }
            else if (rawLine.StartsWith(' ') || string.IsNullOrEmpty(rawLine))
            {
                FlushHunkPairs();
                string content = rawLine.Length > 0 ? rawLine[1..] : string.Empty;
                inline.Add(new GitDiffLine(GitDiffLineKind.Context, content, oldLine, newLine));
                sideBySide.Add(new GitDiffSideBySideRow(oldLine, content, GitDiffLineKind.Context, newLine, content, GitDiffLineKind.Context));
                oldLine++;
                newLine++;
            }
            else if (rawLine.StartsWith("\\ No newline at end of file"))
            {
                // git diff warning
                continue;
            }
        }

        FlushHunkPairs();
    }

    private static void SynthesizeFullAddition(
        string content,
        List<GitDiffLine> inline,
        List<GitDiffSideBySideRow> sideBySide,
        ref int additions)
    {
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            int lineNum = i + 1;
            additions++;
            inline.Add(new GitDiffLine(GitDiffLineKind.Addition, lines[i], null, lineNum));
            sideBySide.Add(new GitDiffSideBySideRow(null, null, GitDiffLineKind.Context, lineNum, lines[i], GitDiffLineKind.Addition));
        }
    }

    private static void SynthesizeFullDeletion(
        string content,
        List<GitDiffLine> inline,
        List<GitDiffSideBySideRow> sideBySide,
        ref int deletions)
    {
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            int lineNum = i + 1;
            deletions++;
            inline.Add(new GitDiffLine(GitDiffLineKind.Deletion, lines[i], lineNum, null));
            sideBySide.Add(new GitDiffSideBySideRow(lineNum, lines[i], GitDiffLineKind.Deletion, null, null, GitDiffLineKind.Context));
        }
    }

    private static void SynthesizeSimpleDiff(
        string oldContent,
        string newContent,
        List<GitDiffLine> inline,
        List<GitDiffSideBySideRow> sideBySide,
        ref int additions,
        ref int deletions)
    {
        var oldLines = oldContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var newLines = newContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int max = Math.Max(oldLines.Length, newLines.Length);
        for (int i = 0; i < max; i++)
        {
            string? o = i < oldLines.Length ? oldLines[i] : null;
            string? n = i < newLines.Length ? newLines[i] : null;

            if (o == n && o != null)
            {
                inline.Add(new GitDiffLine(GitDiffLineKind.Context, o, i + 1, i + 1));
                sideBySide.Add(new GitDiffSideBySideRow(i + 1, o, GitDiffLineKind.Context, i + 1, n, GitDiffLineKind.Context));
            }
            else
            {
                if (o != null)
                {
                    deletions++;
                    inline.Add(new GitDiffLine(GitDiffLineKind.Deletion, o, i + 1, null));
                }
                if (n != null)
                {
                    additions++;
                    inline.Add(new GitDiffLine(GitDiffLineKind.Addition, n, null, i + 1));
                }
                sideBySide.Add(new GitDiffSideBySideRow(
                    o != null ? i + 1 : null,
                    o,
                    o != null ? GitDiffLineKind.Deletion : GitDiffLineKind.Context,
                    n != null ? i + 1 : null,
                    n,
                    n != null ? GitDiffLineKind.Addition : GitDiffLineKind.Context));
            }
        }
    }
}
