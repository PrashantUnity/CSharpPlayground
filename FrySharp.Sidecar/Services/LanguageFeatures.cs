using System.Collections.Concurrent;
using FrySharp.Sidecar.Protocol;
using FrySharp.Sidecar.Sessions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using StreamJsonRpc;

namespace FrySharp.Sidecar.Services;

/// <summary>
/// Language features powered by Roslyn: C# completions, quick info hover, and live diagnostics.
/// </summary>
public sealed class LanguageFeatures
{
    public static readonly TimeSpan DiagnosticsDelay = TimeSpan.FromMilliseconds(400);

    private readonly LanguageRegistry<ILanguageEngine> _languages;
    private readonly RoslynCompilerService _compiler;
    private readonly CSharpCompletionService _completion;
    private readonly CSharpQuickInfoService _quickInfo;
    private readonly ILogger<LanguageFeatures> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingDiagnostics = new(StringComparer.Ordinal);

    public LanguageFeatures(LanguageRegistry<ILanguageEngine> languages, RoslynCompilerService compiler, ILogger<LanguageFeatures> logger)
    {
        _languages = languages;
        _compiler = compiler;
        _completion = new CSharpCompletionService(compiler);
        _quickInfo = new CSharpQuickInfoService(compiler);
        _logger = logger;
    }

    public string LanguageOf(string path) => _languages.LanguageOf(path)?.Id ?? string.Empty;

    public static bool IsCSharp(string path) => HeadlessLanguages.CSharpSourceExtensions.Contains(Path.GetExtension(path));

    public async Task<CompletionResult> CompleteAsync(SidecarSession session, PositionParams request, CancellationToken ct)
    {
        var (text, offset) = TextAt(session, request);
        if (!IsCSharp(request.Path)) return new CompletionResult([]);

        var items = await Task.Run(() => _completion.GetCompletionsAsync(text, offset, ct: ct), ct).ConfigureAwait(false);
        return new CompletionResult(items.Select(ToItem).ToList());
    }

    private static CompletionItem ToItem(CSharpCompletionItem item) => new(
        item.DisplayText,
        item.InsertionText,
        item.Kind.ToString(),
        item.Signature ?? item.ReturnType,
        item.Documentation,
        $"{1000 - Math.Clamp((int)item.Priority, 0, 999):D4}{item.DisplayText}");

    public async Task<HoverResult?> HoverAsync(SidecarSession session, PositionParams request, CancellationToken ct)
    {
        var (text, offset) = TextAt(session, request);
        if (!IsCSharp(request.Path)) return null;

        var info = await Task.Run(() => _quickInfo.GetQuickInfoAsync(text, offset, ct), ct).ConfigureAwait(false);
        return info == null ? null : ToHover(info);
    }

    private static HoverResult? ToHover(CSharpQuickInfo info)
    {
        var parts = new List<string>();
        var signature = string.Concat(info.Signature.Select(s => s.Text));
        if (signature.Length > 0) parts.Add($"```csharp\n{signature}\n```");
        if (info.Container.Count > 0) parts.Add($"*{string.Concat(info.Container.Select(c => c.Text))}*");
        if (!info.Documentation.IsEmpty)
        {
            var summary = string.Concat(info.Documentation.Summary.Select(s => s.Text)).Trim();
            if (summary.Length > 0) parts.Add(summary);
        }
        return parts.Count == 0 ? null : new HoverResult(parts);
    }

    public static bool IsCSharpLanguage(string language) => string.Equals(language, LanguageIds.CSharp, StringComparison.OrdinalIgnoreCase);

    /// <summary>The languages this server has completion, hover and live checks for in a notebook cell.</summary>
    public static bool CanCompleteCells(string language) => IsCSharpLanguage(language) || IsSqlLanguage(language);

    public static bool IsSqlLanguage(string language) => string.Equals(language, LanguageIds.Sql, StringComparison.OrdinalIgnoreCase);

    /// <summary>Completions for a notebook cell: its text after the earlier cells, so what they define is offered.</summary>
    public async Task<CompletionResult> CompleteCellAsync(CellPositionParams request, CancellationToken ct)
    {
        if (IsSqlLanguage(request.Language))
        {
            // the tables and views the cells before create are offered too: they are the same text to the completer
            var joined = string.Concat((request.Context ?? []).Select(cell => cell + "\n"));
            var sqlItems = SqlCompletions.Get(joined + request.Source, joined.Length + request.Offset);
            // a dot-command is offered after the dot that was typed: it inserts its name, not a second dot
            return new CompletionResult(sqlItems.Select(item => ToItem(item) with { InsertText = item.DisplayText.StartsWith('.') ? item.InsertionText[1..] : item.InsertionText }).ToList());
        }

        if (!IsCSharpLanguage(request.Language)) return new CompletionResult([]);
        var (text, offset, _) = CellScript.Compose(request.Context, request.Source, request.Offset);

        var items = await Task.Run(() => _completion.GetCompletionsAsync(text, offset, ct: ct), ct).ConfigureAwait(false);
        return new CompletionResult(items.Select(ToItem).ToList());
    }

    /// <summary>What the symbol under the caret of a notebook cell is, as markdown.</summary>
    public async Task<HoverResult?> HoverCellAsync(CellPositionParams request, CancellationToken ct)
    {
        if (IsSqlLanguage(request.Language))
        {
            var symbol = SqlQuickInfoProvider.ExtractSymbol(request.Source, request.Offset);
            if (symbol is null || SqlQuickInfoProvider.Lookup(symbol) is not { } entry) return null;
            var parts = new List<string> { $"```sql\n{entry.Signature}\n```" };
            if (!string.IsNullOrEmpty(entry.Module)) parts.Add($"*{entry.Module}*");
            parts.Add(entry.Summary);
            return new HoverResult(parts);
        }

        if (!IsCSharpLanguage(request.Language)) return null;
        var (text, offset, _) = CellScript.Compose(request.Context, request.Source, request.Offset);

        var info = await Task.Run(() => _quickInfo.GetQuickInfoAsync(text, offset, ct), ct).ConfigureAwait(false);
        return info == null ? null : ToHover(info);
    }

    // What only joining cells in one text makes wrong is not the cell's: a name two cells both declare (the kernel allows it,
    // the later one wins, and a cell is run again), which the joined text calls a duplicate (CS0128, CS0102...) and then
    // an ambiguity (CS0229) at every use; and statements after a type (CS8803).
    private static readonly HashSet<string> JoinedTextCodes = ["CS0128", "CS0102", "CS0111", "CS0101", "CS0100", "CS0136", "CS0229", "CS0121", "CS8803", "CS8805"];

    /// <summary>
    /// Where a notebook cell goes wrong, in the cell's own lines: its directives (<c>#!python</c>, <c>#!share</c>) in any language,
    /// and for C# what the compiler finds with the earlier cells before it.
    /// </summary>
    public async Task<CellDiagnosticsResult> DiagnoseCellAsync(CellDiagnosticsParams request, CancellationToken ct)
    {
        var diagnostics = new List<DiagnosticDto>();

        var directives = NotebookCellDirectives.Parse(request.Source, _languages, request.Language);
        foreach (var error in directives.Errors)
        {
            // "Line 3: "#!pyton" isn't a directive. ..."
            var match = System.Text.RegularExpressions.Regex.Match(error, @"^Line (\d+): (.*)$");
            var line = match.Success ? int.Parse(match.Groups[1].Value) : 1;
            var message = match.Success ? match.Groups[2].Value : error;
            var lineText = request.Source.Replace("\r\n", "\n").Split('\n').ElementAtOrDefault(line - 1) ?? string.Empty;
            diagnostics.Add(new DiagnosticDto("error", "FRY0001", message, line, 1, line, Math.Max(2, lineText.Length + 1)));
        }

        if (IsCSharpLanguage(directives.LanguageId ?? request.Language) && directives.Errors.Count == 0)
        {
            var (text, _, prefixLines) = CellScript.Compose(request.Context, request.Source, 0);
            var found = await Task.Run(() => _compiler.CheckDiagnostics(text), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            diagnostics.AddRange(found
                .Where(d => d.Severity != DiagnosticSeverity.Hidden && d.Line > prefixLines && !JoinedTextCodes.Contains(d.Id))
                .Select(d => ToDto(d) with
                {
                    StartLine = d.Line - prefixLines,
                    EndLine = Math.Max(d.Line, d.EndLine) - prefixLines
                }));
        }
        return new CellDiagnosticsResult(diagnostics);
    }

    public void DocumentChanged(SidecarSession session, OpenDocument document)
    {
        if (!IsCSharp(document.Path)) return;

        var next = new CancellationTokenSource();
        _pendingDiagnostics.AddOrUpdate(document.Path, next, (_, previous) =>
        {
            previous.Cancel();
            previous.Dispose();
            return next;
        });
        _ = PublishDiagnosticsAsync(session, document, next.Token);
    }

    public void Forget(SidecarSession session, OpenDocument document)
    {
        if (_pendingDiagnostics.TryRemove(document.Path, out var pending))
        {
            pending.Cancel();
            pending.Dispose();
        }
    }

    private async Task PublishDiagnosticsAsync(SidecarSession session, OpenDocument document, CancellationToken ct)
    {
        try
        {
            await Task.Delay(DiagnosticsDelay, ct).ConfigureAwait(false);
            string text;
            int version;
            lock (document.Gate)
            {
                text = document.Text;
                version = document.Version;
            }

            var diagnostics = await Task.Run(() => _compiler.CheckDiagnostics(text), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            session.Notify(ClientMethods.Diagnostics, new DiagnosticsNotification(document.Path, version,
                diagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden).Select(ToDto).ToList()));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Diagnostics for {Path} failed.", document.Path);
        }
    }

    public static DiagnosticDto ToDto(DiagnosticItem item) => new(
        item.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info"
        },
        item.Id,
        item.Message,
        Math.Max(1, item.Line),
        Math.Max(1, item.Column),
        Math.Max(1, item.EndLine > 0 ? item.EndLine : item.Line),
        Math.Max(1, item.EndColumn > 0 ? item.EndColumn : item.Column + 1));

    private static (string Text, int Offset) TextAt(SidecarSession session, PositionParams request)
    {
        if (!session.Documents.TryGetValue(request.Path, out var document))
        {
            throw new LocalRpcException($"'{request.Path}' is not open.") { ErrorCode = ErrorCodes.NotFound };
        }

        lock (document.Gate)
        {
            if (document.OutOfSync || document.Version != request.Version)
            {
                throw new LocalRpcException("The server's copy of the document is not at that version.") { ErrorCode = ErrorCodes.OutOfSync };
            }
            return (document.Text, Math.Clamp(request.Offset, 0, document.Text.Length));
        }
    }
}
