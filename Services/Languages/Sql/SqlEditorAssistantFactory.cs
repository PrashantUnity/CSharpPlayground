using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Attaches code completion and hover Quick Info to SQL editors and notebook cells.
/// </summary>
public sealed class SqlEditorAssistantFactory : IEditorAssistantFactory
{
    public static readonly SqlEditorAssistantFactory Instance = new();

    private readonly SqlCompletionService _completionService = new();

    public IDisposable Attach(TextEditor editor, EditorAssistantContext context)
    {
        var quickInfo = new SqlQuickInfoController(editor, context);
        var completion = new LanguageCompletionController(
            editor,
            context,
            _completionService,
            isImmediateTrigger: (ch, _, _) => ch == '.');

        return new CompositeEditorAssistant(quickInfo, completion);
    }
}

/// <summary>
/// Manages hover tooltips and Ctrl+K Ctrl+I shortcuts for SQL symbols using <see cref="SqlQuickInfoProvider"/>.
/// </summary>
public sealed class SqlQuickInfoController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly TextView _textView;
    private readonly Func<bool>? _isSuppressed;

    private readonly QuickInfoTipControl _tip = new();
    private readonly Popup _popup;
    private readonly DispatcherTimer _dismissTimer;

    private CancellationTokenSource? _requestCts;
    private int _textVersion;
    private Point _pointer;
    private Rect _shownWord;
    private int _shownStart = -1;
    private int _shownLength;
    private bool _isPointerOverTip;
    private bool _chordStarted;

    public SqlQuickInfoController(TextEditor editor, EditorAssistantContext context)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _textView = editor.TextArea.TextView;
        _isSuppressed = context.IsSuppressed;

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementAnchor = PopupAnchor.Bottom,
            PlacementGravity = PopupGravity.Bottom,
            IsLightDismissEnabled = false,
            Child = _tip
        };

        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _dismissTimer.Tick += (_, _) =>
        {
            if (!_isPointerOverTip && !_textView.IsPointerOver) Hide();
        };

        _editor.PointerMoved += OnEditorPointerMoved;
        _editor.PointerExited += OnEditorPointerExited;
        _editor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.Document.TextChanged += OnDocumentTextChanged;

        _tip.PointerEntered += (_, _) => { _isPointerOverTip = true; _dismissTimer.Stop(); };
        _tip.PointerExited += (_, _) => { _isPointerOverTip = false; _dismissTimer.Start(); };
    }

    public void Dispose()
    {
        _dismissTimer.Stop();
        Hide();
        _editor.PointerMoved -= OnEditorPointerMoved;
        _editor.PointerExited -= OnEditorPointerExited;
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnEditorKeyDown);
        if (_editor.Document != null) _editor.Document.TextChanged -= OnDocumentTextChanged;
        _popup.Child = null;
    }

    private void Hide()
    {
        _requestCts?.Cancel();
        _popup.IsOpen = false;
        _shownStart = -1;
        _shownLength = 0;
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        _textVersion++;
        Hide();
    }

    private void OnEditorPointerMoved(object? sender, PointerEventArgs e)
    {
        _pointer = e.GetPosition(_textView);

        if (_popup.IsOpen)
        {
            var padded = _shownWord.Inflate(new Thickness(6, 4));
            if (padded.Contains(_pointer))
            {
                _dismissTimer.Stop();
                return;
            }
            if (!_dismissTimer.IsEnabled) _dismissTimer.Start();
        }

        if (_isSuppressed?.Invoke() == true)
        {
            Hide();
            return;
        }

        var pos = _textView.GetPosition(_pointer + _textView.ScrollOffset);
        if (pos == null)
        {
            if (!_isPointerOverTip) Hide();
            return;
        }

        var offset = _editor.Document.GetOffset(pos.Value.Location);
        TriggerLookup(offset, fromPointer: true);
    }

    private void OnEditorPointerExited(object? sender, PointerEventArgs e)
    {
        if (_popup.IsOpen && !_isPointerOverTip) _dismissTimer.Start();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (_chordStarted)
        {
            _chordStarted = false;
            if (ctrlOrCmd && e.Key == Key.I)
            {
                TriggerLookup(_editor.CaretOffset, fromPointer: false);
                e.Handled = true;
                return;
            }
        }

        if (ctrlOrCmd && e.Key == Key.K)
        {
            _chordStarted = true;
            return;
        }

        if (e.Key == Key.Escape && _popup.IsOpen)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void TriggerLookup(int offset, bool fromPointer)
    {
        _requestCts?.Cancel();
        _requestCts = new CancellationTokenSource();
        var token = _requestCts.Token;
        var version = _textVersion;

        Task.Run(async () =>
        {
            try
            {
                if (fromPointer) await Task.Delay(350, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                var text = _editor.Document.Text;
                var symbol = SqlQuickInfoProvider.ExtractSymbol(text, offset);
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    Dispatcher.UIThread.Post(() => { if (!fromPointer) Hide(); });
                    return;
                }

                var info = SqlQuickInfoProvider.Lookup(symbol);
                if (info == null)
                {
                    Dispatcher.UIThread.Post(() => { if (!fromPointer) Hide(); });
                    return;
                }

                int spanStart = offset;
                while (spanStart > 0 && IsIdentChar(text[spanStart - 1])) spanStart--;

                var csInfo = BuildCSharpQuickInfo(info, spanStart, symbol.Length);

                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested && version == _textVersion)
                    {
                        Show(csInfo, fromPointer);
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SqlQuickInfoController] lookup failed: {ex}");
            }
        }, token);
    }

    private void Show(CSharpQuickInfo info, bool fromPointer)
    {
        if (_editor.Document is not { } document || info.SpanStart < 0 ||
            info.SpanStart + info.SpanLength > document.TextLength) return;

        if (WordBounds(info.SpanStart, info.SpanLength) is not { } word) return;

        if (fromPointer && (!_textView.IsPointerOver || !word.Inflate(2).Contains(_pointer))) return;

        _tip.SetQuickInfo(info, IsDarkTheme());
        _shownStart = info.SpanStart;
        _shownLength = info.SpanLength;
        _shownWord = word;
        _dismissTimer.Stop();

        _popup.IsOpen = false;
        _popup.PlacementRect = word;
        if (_popup.Parent == null) ((ISetLogicalParent)_popup).SetParent(_textView);
        _popup.IsOpen = true;
    }

    private Rect? WordBounds(int start, int length)
    {
        if (!_textView.VisualLinesValid || _editor.Document is not { } document) return null;

        var startLoc = document.GetLocation(start);
        var endLoc = document.GetLocation(start + length);
        var scroll = _textView.ScrollOffset;
        var topLeft = _textView.GetVisualPosition(new TextViewPosition(startLoc), VisualYPosition.LineTop) - scroll;
        var bottom = _textView.GetVisualPosition(new TextViewPosition(startLoc), VisualYPosition.LineBottom).Y - scroll.Y;
        var right = endLoc.Line == startLoc.Line
            ? _textView.GetVisualPosition(new TextViewPosition(endLoc), VisualYPosition.LineTop).X - scroll.X
            : topLeft.X + 8;

        var word = new Rect(new Point(topLeft.X, topLeft.Y), new Point(Math.Max(right, topLeft.X + 1), bottom));
        return new Rect(_textView.Bounds.Size).Intersects(word) ? word : null;
    }

    private bool IsDarkTheme()
    {
        if (Application.Current?.ActualThemeVariant is { } variant)
        {
            return variant == ThemeVariant.Dark;
        }
        return true;
    }

    private static bool IsIdentChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '.';

    private static CSharpQuickInfo BuildCSharpQuickInfo(SqlQuickInfoProvider.QuickInfo info, int spanStart, int spanLength)
    {
        var sig = new List<QuickInfoTextRun> { new(info.Signature, QuickInfoTextKind.Code) };
        var container = new List<QuickInfoTextRun>();
        if (!string.IsNullOrEmpty(info.Module))
        {
            container.Add(new("category ", QuickInfoTextKind.Keyword));
            container.Add(new(info.Module, QuickInfoTextKind.Namespace));
        }

        var summary = new List<QuickInfoTextRun> { new(info.Summary, QuickInfoTextKind.Text) };

        return new CSharpQuickInfo
        {
            SpanStart = spanStart,
            SpanLength = spanLength,
            Signature = sig,
            Container = container,
            Documentation = new QuickInfoDocumentation { Summary = summary }
        };
    }
}
