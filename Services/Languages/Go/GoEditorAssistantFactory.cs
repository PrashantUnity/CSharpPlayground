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

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Attaches code completion and hover Quick Info to Go editors and notebook cells.
/// </summary>
public sealed class GoEditorAssistantFactory : IEditorAssistantFactory
{
    public static readonly GoEditorAssistantFactory Instance = new();

    private readonly GoCompletionService _completionService = new();

    public IDisposable Attach(TextEditor editor, EditorAssistantContext context)
    {
        var quickInfo = new GoQuickInfoController(editor, context);
        var completion = new LanguageCompletionController(
            editor,
            context,
            _completionService,
            isImmediateTrigger: (ch, _, _) => ch == '.');

        return new CompositeEditorAssistant(quickInfo, completion);
    }
}

/// <summary>
/// Manages hover tooltips and Ctrl+K Ctrl+I shortcuts for Go symbols using <see cref="GoQuickInfoProvider"/>.
/// </summary>
public sealed class GoQuickInfoController : IDisposable
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

    public GoQuickInfoController(TextEditor editor, EditorAssistantContext context)
    {
        _editor = editor;
        _textView = editor.TextArea.TextView;
        _isSuppressed = context.IsSuppressed;

        _popup = new Popup
        {
            Child = _tip,
            PlacementTarget = _textView,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.BottomLeft,
            PlacementGravity = PopupGravity.BottomRight,
            PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipY |
                                            PopupPositionerConstraintAdjustment.SlideX |
                                            PopupPositionerConstraintAdjustment.ResizeY,
            VerticalOffset = 2,
            ShouldUseOverlayLayer = true,
            IsLightDismissEnabled = false,
            TakesFocusFromNativeControl = false
        };

        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _dismissTimer.Tick += OnDismissTimerTick;

        _textView.PointerHover += OnPointerHover;
        _textView.PointerMoved += OnPointerMoved;
        _textView.PointerExited += OnPointerExited;
        _textView.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _textView.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        _textView.ScrollOffsetChanged += OnScrollOffsetChanged;
        _textView.DetachedFromVisualTree += OnTextViewDetached;
        _editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _editor.TextChanged += OnTextChanged;
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _tip.PointerEntered += OnTipPointerEntered;
        _tip.PointerExited += OnTipPointerExited;
    }

    public void ShowAtCaret()
    {
        if (_editor.Document == null || _isSuppressed?.Invoke() == true) return;
        var offset = _editor.CaretOffset;
        if ((offset >= _editor.Document.TextLength || !IsIdentChar(_editor.Document.GetCharAt(offset))) &&
            offset > 0 && IsIdentChar(_editor.Document.GetCharAt(offset - 1)))
        {
            offset--;
        }
        Request(offset, fromPointer: false);
    }

    public void Hide()
    {
        _requestCts?.Cancel();
        _dismissTimer.Stop();
        _isPointerOverTip = false;
        _shownStart = -1;
        _popup.IsOpen = false;
    }

    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (_isSuppressed?.Invoke() == true || _editor.Document == null || !_textView.VisualLinesValid) return;
        if (e.GetCurrentPoint(_textView).Properties.IsLeftButtonPressed) return;

        _pointer = e.GetPosition(_textView);
        if (_textView.GetPositionFloor(_pointer + _textView.ScrollOffset) is not { } position) return;

        var offset = _editor.Document.GetOffset(position.Location);
        if (offset >= _editor.Document.TextLength) return;
        if (_popup.IsOpen && offset >= _shownStart && offset < _shownStart + _shownLength) return;

        Request(offset, fromPointer: true);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        _pointer = e.GetPosition(_textView);
        if (!_popup.IsOpen) return;

        if (_shownWord.Inflate(2).Contains(_pointer))
        {
            _dismissTimer.Stop();
        }
        else if (!_isPointerOverTip && !_dismissTimer.IsEnabled)
        {
            _dismissTimer.Start();
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_popup.IsOpen && !_isPointerOverTip && !_dismissTimer.IsEnabled) _dismissTimer.Start();
    }

    private void OnTipPointerEntered(object? sender, PointerEventArgs e)
    {
        _isPointerOverTip = true;
        _dismissTimer.Stop();
    }

    private void OnTipPointerExited(object? sender, PointerEventArgs e)
    {
        _isPointerOverTip = false;
        if (_popup.IsOpen && !_dismissTimer.IsEnabled) _dismissTimer.Start();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => Hide();
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e) => Hide();
    private void OnScrollOffsetChanged(object? sender, EventArgs e) => Hide();
    private void OnTextViewDetached(object? sender, VisualTreeAttachmentEventArgs e) => Hide();
    private void OnTextChanged(object? sender, EventArgs e)
    {
        _textVersion++;
        Hide();
    }
    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (!_isPointerOverTip) Hide();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _popup.IsOpen)
        {
            Hide();
            e.Handled = true;
            return;
        }

        var isCmd = (e.KeyModifiers & KeyModifiers.Control) != 0;
        if (isCmd && e.Key == Key.K)
        {
            _chordStarted = true;
            return;
        }

        if (_chordStarted)
        {
            _chordStarted = false;
            if (isCmd && e.Key == Key.I)
            {
                ShowAtCaret();
                e.Handled = true;
            }
        }
    }

    private void OnDismissTimerTick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();
        if (!_isPointerOverTip) Hide();
    }

    private void Request(int offset, bool fromPointer)
    {
        _requestCts?.Cancel();
        _requestCts = new CancellationTokenSource();
        var token = _requestCts.Token;

        var text = _editor.Text;
        var version = _textVersion;

        Task.Run(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var symbol = GoQuickInfoProvider.ExtractSymbol(text, offset);
                if (symbol == null || token.IsCancellationRequested) return;

                var info = GoQuickInfoProvider.Lookup(symbol);
                if (info == null || token.IsCancellationRequested) return;

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
                Debug.WriteLine($"[GoQuickInfoController] lookup failed: {ex}");
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

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static CSharpQuickInfo BuildCSharpQuickInfo(GoQuickInfoProvider.QuickInfo info, int spanStart, int spanLength)
    {
        var sig = new List<QuickInfoTextRun> { new(info.Signature, QuickInfoTextKind.Code) };
        var container = new List<QuickInfoTextRun>();
        if (info.Package != null)
        {
            container.Add(new("package ", QuickInfoTextKind.Keyword));
            container.Add(new(info.Package, QuickInfoTextKind.Namespace));
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

    public void Dispose()
    {
        Hide();
        _dismissTimer.Tick -= OnDismissTimerTick;

        _textView.PointerHover -= OnPointerHover;
        _textView.PointerMoved -= OnPointerMoved;
        _textView.PointerExited -= OnPointerExited;
        _textView.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _textView.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        _textView.ScrollOffsetChanged -= OnScrollOffsetChanged;
        _textView.DetachedFromVisualTree -= OnTextViewDetached;
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _editor.TextChanged -= OnTextChanged;
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _tip.PointerEntered -= OnTipPointerEntered;
        _tip.PointerExited -= OnTipPointerExited;

        if (_popup.Parent != null) ((ISetLogicalParent)_popup).SetParent(null);
    }
}
