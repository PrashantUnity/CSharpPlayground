using System.Collections.Concurrent;
using FrySharp.Sidecar.Protocol;
using FrySharp.Sidecar.Sessions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
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
        return new CompletionResult(items.Select(item => new CompletionItem(
            item.DisplayText,
            item.InsertionText,
            item.Kind.ToString(),
            item.Signature ?? item.ReturnType,
            item.Documentation,
            $"{1000 - Math.Clamp((int)item.Priority, 0, 999):D4}{item.DisplayText}")).ToList());
    }

    public async Task<HoverResult?> HoverAsync(SidecarSession session, PositionParams request, CancellationToken ct)
    {
        var (text, offset) = TextAt(session, request);
        if (!IsCSharp(request.Path)) return null;

        var info = await Task.Run(() => _quickInfo.GetQuickInfoAsync(text, offset, ct), ct).ConfigureAwait(false);
        if (info == null) return null;

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
