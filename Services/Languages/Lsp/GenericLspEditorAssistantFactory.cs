using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaEdit;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Lsp;

/// <summary>
/// Universal editor assistant factory for external language servers.
/// Launches and binds an LSP server to AvaloniaEdit's code completion, Quick Info hover,
/// and document synchronization.
/// </summary>
public sealed class GenericLspEditorAssistantFactory : IEditorAssistantFactory, IDisposable
{
    private readonly string _command;
    private readonly IReadOnlyList<string> _args;
    private readonly string _languageId;
    private readonly ConcurrentDictionary<string, LspClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public GenericLspEditorAssistantFactory(string command, IEnumerable<string>? args = null, string? languageId = null)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _args = args != null ? new List<string>(args) : Array.Empty<string>();
        _languageId = languageId ?? Path.GetFileNameWithoutExtension(command).ToLowerInvariant();
    }

    public IDisposable Attach(TextEditor editor, EditorAssistantContext context)
    {
        string rootPath = Directory.GetCurrentDirectory();
        var client = _clients.GetOrAdd(rootPath, path =>
        {
            var lsp = new LspClient(_command, _args, path);
            _ = Task.Run(async () =>
            {
                try
                {
                    await lsp.StartAsync(path);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GenericLsp] StartAsync failed: {ex.Message}");
                }
            });
            return lsp;
        });

        string docPath = Path.Combine(rootPath, $"temp_{Guid.NewGuid():N}.{_languageId}");
        int docVersion = 1;

        string initialText = editor.Text;
        // Open doc in LSP
        _ = Task.Run(async () =>
        {
            try
            {
                await client.DidOpenAsync(docPath, _languageId, initialText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GenericLsp] DidOpenAsync failed: {ex.Message}");
            }
        });

        // Debounced text change synchronization
        var changeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        changeTimer.Tick += (s, e) =>
        {
            changeTimer.Stop();
            var text = editor.Text;
            var ver = Interlocked.Increment(ref docVersion);
            _ = Task.Run(async () =>
            {
                try
                {
                    await client.DidChangeAsync(docPath, ver, text);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GenericLsp] DidChangeAsync failed: {ex.Message}");
                }
            });
        };

        void OnTextChanged(object? sender, EventArgs e)
        {
            changeTimer.Stop();
            changeTimer.Start();
        }

        editor.Document.TextChanged += OnTextChanged;

        // Completion service adapter
        var completionService = new LspCompletionAdapter(client, docPath);
        var completion = new LanguageCompletionController(
            editor,
            context,
            completionService,
            isImmediateTrigger: (ch, _, _) => ch == '.' || ch == ':');

        // QuickInfo hover controller adapter
        var quickInfo = new LanguageQuickInfoController(
            editor,
            context,
            (text, offset) =>
            {
                if (offset < 0 || offset >= text.Length) return null;
                int line = 0, col = 0, currentOffset = 0;
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    int lineLen = lines[i].Length + 1; // +1 for \n
                    if (currentOffset + lineLen > offset)
                    {
                        line = i;
                        col = offset - currentOffset;
                        break;
                    }
                    currentOffset += lineLen;
                }
                try
                {
                    return client.GetHoverAsync(docPath, line, col, offset).GetAwaiter().GetResult();
                }
                catch
                {
                    return null;
                }
            });

        var composite = new CompositeEditorAssistant(quickInfo, completion);

        return new AssistantRegistration(() =>
        {
            editor.Document.TextChanged -= OnTextChanged;
            changeTimer.Stop();
            _ = Task.Run(async () =>
            {
                try
                {
                    await client.DidCloseAsync(docPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GenericLsp] DidCloseAsync failed: {ex.Message}");
                }
            });
            composite.Dispose();
        });
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
    }

    private sealed class LspCompletionAdapter : ILanguageCompletionService
    {
        private readonly LspClient _client;
        private readonly string _docPath;

        public LspCompletionAdapter(LspClient client, string docPath)
        {
            _client = client;
            _docPath = docPath;
        }

        public async Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
            string code,
            int caretOffset,
            EditorAssistantContext context,
            CancellationToken ct = default)
        {
            int line = 0, col = 0;
            int currentOffset = 0;
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int lineLen = lines[i].Length + 1; // +1 for \n
                if (currentOffset + lineLen > caretOffset)
                {
                    line = i;
                    col = caretOffset - currentOffset;
                    break;
                }
                currentOffset += lineLen;
            }

            return await _client.GetCompletionsAsync(_docPath, line, col, ct);
        }
    }

    private sealed class AssistantRegistration : IDisposable
    {
        private Action? _disposeAction;

        public AssistantRegistration(Action disposeAction)
        {
            _disposeAction = disposeAction;
        }

        public void Dispose()
        {
            var action = Interlocked.Exchange(ref _disposeAction, null);
            action?.Invoke();
        }
    }
}
