using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public class CSharpEditorCompletionController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly Func<RoslynCompilerService>? _compilerProvider;
    private CSharpCompletionService? _completionService;
    private CompletionWindow? _completionWindow;
    private CancellationTokenSource? _queryCts;
    private readonly DispatcherTimer _debounceTimer;

    public ExecutionLanguageMode LanguageMode { get; set; } = ExecutionLanguageMode.Statements;

    public CSharpEditorCompletionController(TextEditor editor, RoslynCompilerService compilerService)
        : this(editor, () => compilerService)
    {
    }

    public CSharpEditorCompletionController(TextEditor editor, Func<RoslynCompilerService> compilerProvider)
    {
        _editor = editor;
        _compilerProvider = compilerProvider;

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(160)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            TriggerCompletion(explicitTrigger: false);
        };

        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.KeyDown += OnKeyDown;
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completionWindow != null && !string.IsNullOrEmpty(e.Text))
        {
            // If user types a dot while completion is open, close so dot triggers fresh member access
            if (e.Text == ".")
            {
                _completionWindow.Close();
            }
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;

        var ch = e.Text[0];

        // 1. Immediate trigger on dot '.' for member access
        if (ch == '.')
        {
            _debounceTimer.Stop();
            TriggerCompletion(explicitTrigger: true);
            return;
        }

        // 2. Debounced trigger when typing alphanumeric identifier characters
        if (char.IsLetterOrDigit(ch) || ch == '_')
        {
            if (_completionWindow == null)
            {
                int offset = _editor.CaretOffset;
                var text = _editor.Text ?? string.Empty;
                int wordStart = offset - 1;
                while (wordStart > 0 && (char.IsLetterOrDigit(text[wordStart - 1]) || text[wordStart - 1] == '_'))
                {
                    wordStart--;
                }

                int wordLen = offset - wordStart;
                if (wordLen >= 1)
                {
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
                }
            }
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+Space or Cmd+Space manual trigger
        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (isModifier && e.Key == Key.Space)
        {
            _debounceTimer.Stop();
            TriggerCompletion(explicitTrigger: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _completionWindow != null)
        {
            _completionWindow.Close();
            e.Handled = true;
            return;
        }
    }

    public void TriggerCompletion(bool explicitTrigger = false)
    {
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        var token = _queryCts.Token;

        var text = _editor.Text ?? string.Empty;
        int caretOffset = _editor.CaretOffset;
        if (caretOffset < 0 || caretOffset > text.Length) return;

        bool isDot = caretOffset > 0 && text[caretOffset - 1] == '.';

        // Calculate start offset of current token/member
        int startOffset;
        string initialQuery = string.Empty;

        if (isDot)
        {
            startOffset = caretOffset;
        }
        else
        {
            int i = caretOffset;
            while (i > 0 && (char.IsLetterOrDigit(text[i - 1]) || text[i - 1] == '_'))
            {
                i--;
            }
            startOffset = i;
            if (caretOffset > startOffset)
            {
                initialQuery = text.Substring(startOffset, caretOffset - startOffset);
            }
        }

        var mode = LanguageMode;

        _ = Task.Run(async () =>
        {
            try
            {
                if (_completionService == null && _compilerProvider != null)
                {
                    var compiler = _compilerProvider();
                    _completionService = new CSharpCompletionService(compiler);
                }

                if (_completionService == null) return;

                var items = await _completionService.GetCompletionsAsync(text, caretOffset, mode, token);

                if (token.IsCancellationRequested || items.Count == 0) return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;

                    // Support focus within editor or its TextArea child
                    bool hasFocus = _editor.IsKeyboardFocusWithin || _editor.TextArea.IsFocused || explicitTrigger;
                    if (!hasFocus) return;

                    _completionWindow?.Close();

                    _completionWindow = new CompletionWindow(_editor.TextArea)
                    {
                        StartOffset = startOffset,
                        CloseAutomatically = true,
                        CloseWhenCaretAtBeginning = !isDot, // For dot completion, do NOT close when caret is at beginning!
                        ExpectInsertionBeforeStart = false,
                        MaxHeight = 280,
                        MaxWidth = 480
                    };

                    // Material Design 3 Expressive dark styling
                    _completionWindow.CompletionList.Background = new SolidColorBrush(Color.Parse("#14171F"));
                    _completionWindow.CompletionList.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
                    _completionWindow.CompletionList.BorderBrush = new SolidColorBrush(Color.Parse("#30363D"));
                    _completionWindow.CompletionList.BorderThickness = new Thickness(1);
                    _completionWindow.CompletionList.CornerRadius = new CornerRadius(8);

                    var data = _completionWindow.CompletionList.CompletionData;
                    data.Clear();
                    foreach (var item in items)
                    {
                        data.Add(new CSharpCompletionData(item));
                    }

                    _completionWindow.Closed += (s, e) =>
                    {
                        _completionWindow = null;
                    };

                    _completionWindow.Show();

                    // Immediately select best matching item upon opening
                    var currentCaret = _editor.CaretOffset;
                    var currentText = _editor.Text ?? string.Empty;
                    var effectiveQuery = initialQuery;
                    if (currentCaret > startOffset && currentCaret <= currentText.Length)
                    {
                        effectiveQuery = currentText.Substring(startOffset, currentCaret - startOffset);
                    }

                    if (!string.IsNullOrEmpty(effectiveQuery))
                    {
                        _completionWindow.CompletionList.SelectItem(effectiveQuery);
                    }
                    else if (data.Count > 0)
                    {
                        _completionWindow.CompletionList.SelectedItem = data[0];
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Debouncing cancellation
            }
            catch
            {
                // Resiliently ignore UI threading or window dismiss errors
            }
        }, token);
    }

    public void Dispose()
    {
        _debounceTimer.Stop();
        _queryCts?.Cancel();
        _completionWindow?.Close();

        _editor.TextArea.TextEntered -= OnTextEntered;
        _editor.TextArea.TextEntering -= OnTextEntering;
        _editor.KeyDown -= OnKeyDown;
    }
}
