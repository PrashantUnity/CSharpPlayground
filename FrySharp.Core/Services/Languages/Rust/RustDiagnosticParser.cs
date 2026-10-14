using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Reads Problems out of a failed Rust build or run: rustc's messages (<c>error[E0308]: …</c> with its <c>--&gt; file:line:col</c>
/// location and caret line), cargo's own errors (an unknown crate, a missing linker), and a program's panics.
/// </summary>
public sealed partial class RustDiagnosticParser : IDiagnosticParser
{
    private const int MaxDiagnostics = 200;
    private const int MaxSpanLookahead = 14;

    // error[E0308]: mismatched types   |   warning: unused variable: `x`
    [GeneratedRegex(@"^(?<severity>error|warning)(?:\[(?<code>[A-Za-z]\d{4})\])?:\s*(?<message>.+?)\s*$")]
    private static partial Regex HeaderRegex();

    //  --> /path/main.rs:2:18
    [GeneratedRegex(@"^\s*-->\s+(?<file>.+?):(?<line>\d+):(?<col>\d+)\s*$")]
    private static partial Regex LocationRegex();

    //    |     ---   ^^^^^^ expected `i32`, found `&str`   (a line under the code: no line number before the bar)
    [GeneratedRegex(@"^\s+\|(?<rest>.*)$")]
    private static partial Regex MarkerLineRegex();

    [GeneratedRegex(@"\^+")]
    private static partial Regex CaretRunRegex();

    // Where the snippet ends: another sub-message (note:, help:, = note:, a second -->).
    [GeneratedRegex(@"^\s*(?:note|help|warning|error)\b|^\s*=\s|^\s*-->")]
    private static partial Regex EndOfSnippetRegex();

    [GeneratedRegex(@"#\[(?:warn|deny|forbid)\((?<lint>[A-Za-z0-9_:]+)\)\]")]
    private static partial Regex LintRegex();

    [GeneratedRegex(@"^(?:aborting due to|could not compile|Some errors have detailed|`[^`]+` \((?:bin|lib|test|example|bench)\b.*generated \d+ warning)")]
    private static partial Regex SummaryRegex();

    [GeneratedRegex(@"^thread '(?<thread>[^']*)'(?: \(\d+\))? panicked at (?:'(?<old>.*)', )?(?<file>.+?):(?<line>\d+):(?<col>\d+):?\s*$")]
    private static partial Regex PanicRegex();

    [GeneratedRegex(@"^thread '[^']*'(?: \(\d+\))? has overflowed its stack")]
    private static partial Regex StackOverflowRegex();

    [GeneratedRegex("""linker ['`"](?<linker>[^'`"]+)['`"] not found""")]
    private static partial Regex LinkerNotFoundRegex();

    [GeneratedRegex("""linking with ['`"](?<linker>[^'`"]+)['`"] failed""")]
    private static partial Regex LinkFailedRegex();

    [GeneratedRegex(@"no matching package named `(?<name>[^`]+)`|failed to select a version for(?: the requirement)? `(?<name2>[A-Za-z0-9_\-]+)")]
    private static partial Regex UnknownCrateRegex();

    // Rust 1.9x: "= help: if you wanted to use a crate named `rand`, use `cargo add rand` to add it to your `Cargo.toml`"
    [GeneratedRegex(@"use `cargo add (?<name>[A-Za-z0-9_\-]+)`")]
    private static partial Regex CargoAddHintRegex();

    [GeneratedRegex(@"(?:use of undeclared crate or module|use of unresolved module or unlinked crate|can't find crate for|maybe a missing crate) `(?<name>[A-Za-z_][A-Za-z0-9_]*)`|unresolved import `(?<import>[A-Za-z_][A-Za-z0-9_]*)`")]
    private static partial Regex MissingCrateRegex();

    [GeneratedRegex(@"(?im)^\s*(?:Compiling|Updating|Locking|Downloading|Downloaded|Blocking|Adding|Fresh)\s|error: could not compile|^error: (?:failed to|no matching package|linker|linking with|could not find|unable to|package)")]
    private static partial Regex BuildOutputRegex();

    private static readonly HashSet<string> NotCrates = new(StringComparer.Ordinal)
    {
        "std", "core", "alloc", "crate", "self", "super", "Self", "proc_macro", "test"
    };

    private sealed class Block
    {
        public required bool IsError { get; init; }
        public required string Message { get; init; }
        public string? Code { get; init; }
        public bool IsSummary { get; init; }
        public List<string> Body { get; } = new();
        public string? File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int LocationIndex { get; set; } = -1;
    }

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var blocks = ReadBlocks(lines);
        var missingDependency = FindMissingDependency(lines, sourceFilePath);

        var diagnostics = new List<DiagnosticItem>();
        AddLocated(diagnostics, blocks, sourceFilePath);
        if (diagnostics.Count == 0) AddInOtherFiles(diagnostics, blocks, sourceFilePath);
        if (diagnostics.Count == 0 && BuildOutputRegex().IsMatch(output)) AddBuildLevel(diagnostics, blocks, sourceFilePath);
        if (diagnostics.Count == 0) AddRuntimeFailures(diagnostics, lines, sourceFilePath);

        return new DiagnosticParseResult(diagnostics, missingDependency);
    }

    private static List<Block> ReadBlocks(string[] lines)
    {
        var blocks = new List<Block>();
        Block? current = null;
        foreach (var raw in lines)
        {
            var header = HeaderRegex().Match(raw);
            if (header.Success)
            {
                var message = header.Groups["message"].Value;
                current = new Block
                {
                    IsError = header.Groups["severity"].Value == "error",
                    Message = message,
                    Code = header.Groups["code"].Success ? header.Groups["code"].Value : null,
                    IsSummary = SummaryRegex().IsMatch(message)
                };
                blocks.Add(current);
                continue;
            }

            if (current == null) continue;
            current.Body.Add(raw);
            if (current.LocationIndex < 0)
            {
                var location = LocationRegex().Match(raw);
                if (location.Success)
                {
                    current.File = location.Groups["file"].Value.Trim();
                    current.Line = int.Parse(location.Groups["line"].Value);
                    current.Column = int.Parse(location.Groups["col"].Value);
                    current.LocationIndex = current.Body.Count - 1;
                }
            }
        }

        return blocks;
    }

    // Errors and warnings rustc placed in the file that ran.
    private static void AddLocated(List<DiagnosticItem> diagnostics, List<Block> blocks, string sourceFilePath)
    {
        foreach (var block in blocks)
        {
            if (diagnostics.Count >= MaxDiagnostics) return;
            if (block.IsSummary || block.File == null || !SamePath(block.File, sourceFilePath)) continue;

            var (length, label) = PrimarySpan(block);
            var message = block.Message;
            if (label.Length > 0 &&
                !label.StartsWith("help:", StringComparison.Ordinal) &&
                !label.StartsWith("note:", StringComparison.Ordinal) &&
                message.IndexOf(label, StringComparison.OrdinalIgnoreCase) < 0)
            {
                message = $"{message}: {label}";
            }

            diagnostics.Add(new DiagnosticItem
            {
                Id = block.Code ?? LintOf(block) ?? (block.IsError ? "RUSTC" : "RUSTC_WARN"),
                Message = message,
                Severity = block.IsError ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                Line = block.Line,
                Column = block.Column,
                EndLine = block.Line,
                EndColumn = block.Column + Math.Max(length, 1)
            });
        }
    }

    // A module file next to the script (mod helper;) has the error: Problems belongs to the script, so point at its first line.
    private static void AddInOtherFiles(List<DiagnosticItem> diagnostics, List<Block> blocks, string sourceFilePath)
    {
        foreach (var block in blocks)
        {
            if (diagnostics.Count >= 5) return;
            if (block.IsSummary || !block.IsError || block.File == null || IsToolchainPath(block.File)) continue;

            diagnostics.Add(new DiagnosticItem
            {
                Id = block.Code ?? "RUSTC",
                Message = $"{Path.GetFileName(block.File.Replace('\\', '/'))}:{block.Line}:{block.Column}: {block.Message}",
                Severity = DiagnosticSeverity.Error,
                Line = 1,
                Column = 1,
                EndLine = 1,
                EndColumn = 2
            });
        }
    }

    // Errors with no place in any file: cargo couldn't resolve a crate, or the linker is missing.
    private static void AddBuildLevel(List<DiagnosticItem> diagnostics, List<Block> blocks, string sourceFilePath)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in blocks)
        {
            if (diagnostics.Count >= 10) return;
            if (block.IsSummary || !block.IsError || block.File != null) continue;

            var text = string.Join('\n', block.Body.Prepend(block.Message));
            var line = 1;
            var id = "CARGO";
            var message = block.Message;

            var linker = LinkerNotFoundRegex().Match(block.Message);
            var linkFailed = LinkFailedRegex().Match(block.Message);
            if (linker.Success)
            {
                id = "RUSTC_LINKER";
                message = RustGuidance.LinkerHint(linker.Groups["linker"].Value);
            }
            else if (linkFailed.Success)
            {
                id = "RUSTC_LINKER";
                message = $"{block.Message}. {RustGuidance.LinkerHint(linkFailed.Groups["linker"].Value)}";
            }
            else if (UnknownCrateRegex().Match(text) is { Success: true } crate)
            {
                var name = crate.Groups["name"].Success ? crate.Groups["name"].Value : crate.Groups["name2"].Value;
                line = FindCrateLine(sourceFilePath, name);
                message = $"{block.Message}. Check the crate's name and version in your // #crate: line.";
            }

            if (!seen.Add(message)) continue;
            diagnostics.Add(new DiagnosticItem
            {
                Id = id,
                Message = message,
                Severity = DiagnosticSeverity.Error,
                Line = line,
                Column = 1,
                EndLine = line,
                EndColumn = 2
            });
        }
    }

    private static void AddRuntimeFailures(List<DiagnosticItem> diagnostics, string[] lines, string sourceFilePath)
    {
        for (var i = 0; i < lines.Length && diagnostics.Count < 5; i++)
        {
            var line = lines[i].TrimEnd();

            var panic = PanicRegex().Match(line);
            if (panic.Success)
            {
                var message = panic.Groups["old"].Success ? panic.Groups["old"].Value : ReadPanicMessage(lines, i + 1);
                var file = panic.Groups["file"].Value;
                var lineNumber = int.Parse(panic.Groups["line"].Value);
                var column = int.Parse(panic.Groups["col"].Value);
                var inSource = SamePath(file, sourceFilePath);

                diagnostics.Add(new DiagnosticItem
                {
                    Id = "PANIC",
                    Message = inSource
                        ? $"panic: {message}"
                        : $"panic in {Path.GetFileName(file.Replace('\\', '/'))}:{lineNumber}:{column}: {message}",
                    Severity = DiagnosticSeverity.Error,
                    Line = inSource ? lineNumber : 1,
                    Column = inSource ? column : 1,
                    EndLine = inSource ? lineNumber : 1,
                    EndColumn = (inSource ? column : 1) + 1
                });
                continue;
            }

            if (StackOverflowRegex().IsMatch(line))
            {
                diagnostics.Add(new DiagnosticItem
                {
                    Id = "STACK_OVERFLOW",
                    Message = "stack overflow: the program used more stack than it has (look for recursion that never ends)",
                    Severity = DiagnosticSeverity.Error,
                    Line = 1, Column = 1, EndLine = 1, EndColumn = 2
                });
            }
        }

        if (diagnostics.Count > 0) return;

        // fn main() -> Result<…> that returned Err prints "Error: <Debug of the error>" and exits with code 1.
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].TrimEnd();
            if (line.Length == 0) continue;
            if (line.StartsWith("Error: ", StringComparison.Ordinal))
            {
                diagnostics.Add(new DiagnosticItem
                {
                    Id = "RUST_ERROR",
                    Message = $"main returned an error: {line["Error: ".Length..]}",
                    Severity = DiagnosticSeverity.Error,
                    Line = 1, Column = 1, EndLine = 1, EndColumn = 2
                });
            }

            break;
        }
    }

    // The message of a panic that prints on the lines after "panicked at": everything up to the backtrace note.
    private static string ReadPanicMessage(string[] lines, int start)
    {
        var parts = new List<string>();
        for (var i = start; i < lines.Length && parts.Count < 8; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 ||
                line.StartsWith("note: run with", StringComparison.Ordinal) ||
                line.StartsWith("stack backtrace:", StringComparison.Ordinal) ||
                line.StartsWith("thread '", StringComparison.Ordinal))
            {
                break;
            }

            parts.Add(line);
        }

        return parts.Count == 0 ? "the program panicked" : string.Join(" ", parts);
    }

    // The caret run under the primary span and the label written after it.
    private static (int Length, string Label) PrimarySpan(Block block)
    {
        if (block.LocationIndex < 0) return (0, string.Empty);

        var body = block.Body;
        for (var i = block.LocationIndex + 1; i < body.Count && i <= block.LocationIndex + MaxSpanLookahead; i++)
        {
            var line = body[i];
            if (string.IsNullOrWhiteSpace(line) || EndOfSnippetRegex().IsMatch(line)) break;

            var marker = MarkerLineRegex().Match(line);
            if (!marker.Success) continue;

            var rest = marker.Groups["rest"].Value;
            var runs = CaretRunRegex().Matches(rest);
            if (runs.Count == 0) continue;

            // The text after "| " lines up with the code, so a run at index n marks column n.
            Match run = runs[0];
            foreach (Match candidate in runs)
            {
                if (candidate.Index == block.Column)
                {
                    run = candidate;
                    break;
                }
            }

            return (run.Length, rest[(run.Index + run.Length)..].Trim());
        }

        return (0, string.Empty);
    }

    private static string? LintOf(Block block)
    {
        foreach (var line in block.Body)
        {
            var lint = LintRegex().Match(line);
            if (lint.Success) return lint.Groups["lint"].Value;
        }

        return null;
    }

    private static string? FindMissingDependency(string[] lines, string sourceFilePath)
    {
        string? name = null;
        foreach (var line in lines)
        {
            var hint = CargoAddHintRegex().Match(line);
            if (hint.Success)
            {
                name = hint.Groups["name"].Value;
                break;
            }
        }

        if (name == null)
        {
            foreach (var line in lines)
            {
                var missing = MissingCrateRegex().Match(line);
                if (!missing.Success) continue;
                name = missing.Groups["name"].Success ? missing.Groups["name"].Value : missing.Groups["import"].Value;
                break;
            }
        }

        if (name == null || NotCrates.Contains(name)) return null;

        // A crate the script already asks for isn't missing: something else is wrong with it.
        var declared = DeclaredCrates(sourceFilePath);
        return declared.Contains(CrateKey(name)) ? null : name;
    }

    private static HashSet<string> DeclaredCrates(string sourceFilePath)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (File.Exists(sourceFilePath))
            {
                foreach (var crate in RustDirectives.Parse(File.ReadAllText(sourceFilePath)).Crates) declared.Add(CrateKey(crate.Name));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable: treat nothing as declared.
        }

        return declared;
    }

    private static string CrateKey(string name) => name.Replace('-', '_').ToLowerInvariant();

    // The line of the // #crate: comment for a crate cargo couldn't find, so the Problem lands on it.
    private static int FindCrateLine(string sourceFilePath, string name)
    {
        try
        {
            if (!File.Exists(sourceFilePath)) return 1;
            var lines = File.ReadAllLines(sourceFilePath);
            for (var i = 0; i < lines.Length; i++)
            {
                if (RustDirectives.TryParseCrateLine(lines[i], out var crate) && CrateKey(crate.Name) == CrateKey(name)) return i + 1;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall through to line 1.
        }

        return 1;
    }

    private static bool IsToolchainPath(string file)
    {
        var normalized = file.Replace('\\', '/');
        return normalized.Contains("/.cargo/registry/", StringComparison.Ordinal) ||
               normalized.Contains("/.cargo/git/", StringComparison.Ordinal) ||
               normalized.StartsWith("/rustc/", StringComparison.Ordinal) ||
               normalized.Contains("/lib/rustlib/", StringComparison.Ordinal);
    }

    private static bool SamePath(string file, string sourceFilePath)
    {
        var a = file.Replace('\\', '/').Trim();
        var b = sourceFilePath.Replace('\\', '/').Trim();
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        return !IsToolchainPath(a) &&
               string.Equals(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
    }
}
