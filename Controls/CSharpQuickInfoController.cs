using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

/// <summary>
/// Quick Info for a C# editor: rest the pointer on a symbol (or press Ctrl+K Ctrl+I, as in VS Code, for the one at the
/// caret) and a card under it shows the signature, where it's declared and its documentation. The analysis runs on a
/// background thread. The card stays open while the pointer is on the word or the card, and closes on typing, clicking,
/// scrolling or Escape.
/// </summary>
public sealed class CSharpQuickInfoController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly TextView _textView;
    private readonly Lazy<CSharpQuickInfoService> _service;
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

    /// <summary>
    /// Optional code that precedes this editor's text but isn't in its document (earlier notebook cells), so names they
    /// declare resolve. Only the analysis sees it; offsets and placement stay in this editor's own text.
    /// </summary>
    public Func<string>? PrecedingContextProvider { get; set; }

    /// <summary>When this returns true no card opens, e.g. while the debugger is paused and its data tip owns the hover.</summary>
    public Func<bool>? IsSuppressed { get; set; }

    /// <param name="editor">The editor to watch.</param>
    /// <param name="serviceProvider">Creates the analysis service; called once, on a background thread.</param>
    public CSharpQuickInfoController(TextEditor editor, Func<CSharpQuickInfoService> serviceProvider)
    {
        _editor = editor;
        _textView = editor.TextArea.TextView;
        _service = new Lazy<CSharpQuickInfoService>(serviceProvider, LazyThreadSafetyMode.ExecutionAndPublication);

        // In the window's overlay layer, not a native popup window: no focus is taken from the editor.
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

        // Moving the pointer from the word down to the card crosses a gap; wait a moment before closing.
        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _dismissTimer.Tick += OnDismissTimerTick;

        _textView.PointerHover += OnPointerHover;
        _textView.PointerMoved += OnPointerMoved;
        _textView.PointerExited += OnPointerExited;
        _textView.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        // A notebook cell doesn't scroll itself: the wheel moves the page around it, and the word with it.
        _textView.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        _textView.ScrollOffsetChanged += OnScrollOffsetChanged;
        _textView.DetachedFromVisualTree += OnTextViewDetached;
        _editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _editor.TextChanged += OnTextChanged;
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _tip.PointerEntered += OnTipPointerEntered;
        _tip.PointerExited += OnTipPointerExited;
    }

    /// <summary>Shows the card for the symbol at (or just before) the caret.</summary>
    public void ShowAtCaret()
    {
        var document = _editor.Document;
        if (document == null || IsSuppressed?.Invoke() == true) return;

        // The caret usually sits right after the name (`Console.WriteLine|(`), so fall back to the character before it.
        var offset = _editor.CaretOffset;
        if ((offset >= document.TextLength || !IsIdentifierCharacter(document.GetCharAt(offset))) &&
            offset > 0 && IsIdentifierCharacter(document.GetCharAt(offset - 1)))
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
        if (IsSuppressed?.Invoke() == true || _editor.Document == null || !_textView.VisualLinesValid) return;
        if (e.GetCurrentPoint(_textView).Properties.IsLeftButtonPressed) return;

        _pointer = e.GetPosition(_textView);
        // GetPositionFloor works in document coordinates: the text view's own point plus how far it's scrolled.
        if (_textView.GetPositionFloor(_pointer + _textView.ScrollOffset) is not { } position) return;

        var offset = _editor.Document.GetOffset(position.Location);
        if (offset >= _editor.Document.TextLength) return;
        if (_popup.IsOpen && offset >= _shownStart && offset < _shownStart + _shownLength) return;

        Request(offset, fromPointer: true);
    }

    private void Request(int offset, bool fromPointer)
    {
        _requestCts?.Cancel();
        var cts = _requestCts = new CancellationTokenSource();
        var token = cts.Token;

        var context = PrecedingContextProvider?.Invoke();
        var contextLength = string.IsNullOrEmpty(context) ? 0 : context.Length + 1;
        var text = _editor.Text ?? string.Empty;
        var analysisText = contextLength == 0 ? text : context + "\n" + text;
        var version = _textVersion;

        _ = Task.Run(async () =>
        {
            try
            {
                var info = await _service.Value.GetQuickInfoAsync(analysisText, contextLength + offset, token);
                if (info == null || token.IsCancellationRequested) return;

                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested && version == _textVersion)
                    {
                        Show(info, info.SpanStart - contextLength, fromPointer);
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpQuickInfoController] Quick info failed: {ex}");
            }
        }, token);
    }

    private void Show(CSharpQuickInfo info, int start, bool fromPointer)
    {
        if (_editor.Document is not { } document || start < 0 || start + info.SpanLength > document.TextLength) return;
        if (WordBounds(start, info.SpanLength) is not { } word) return;

        // The pointer moved on while the analysis ran.
        if (fromPointer && (!_textView.IsPointerOver || !word.Inflate(2).Contains(_pointer))) return;

        _tip.SetQuickInfo(info, IsDarkTheme());
        _shownStart = start;
        _shownLength = info.SpanLength;
        _shownWord = word;
        _dismissTimer.Stop();

        // Reopen so the popup is placed against the new word.
        _popup.IsOpen = false;
        _popup.PlacementRect = word;
        // The popup lives outside the visual tree, so give it the editor as logical parent: the card then resolves
        // the app's theme resources (the M3 brushes) and theme variant the way the editor does.
        if (_popup.Parent == null) ((ISetLogicalParent)_popup).SetParent(_textView);
        _popup.IsOpen = true;
    }

    // The word's box in text view coordinates, or null when it's scrolled out of view.
    private Rect? WordBounds(int start, int length)
    {
        if (!_textView.VisualLinesValid || _editor.Document is not { } document) return null;

        var startLocation = document.GetLocation(start);
        var endLocation = document.GetLocation(start + length);
        var scroll = _textView.ScrollOffset;
        var topLeft = _textView.GetVisualPosition(new TextViewPosition(startLocation), VisualYPosition.LineTop) - scroll;
        var bottom = _textView.GetVisualPosition(new TextViewPosition(startLocation), VisualYPosition.LineBottom).Y - scroll.Y;
        var right = endLocation.Line == startLocation.Line
            ? _textView.GetVisualPosition(new TextViewPosition(endLocation), VisualYPosition.LineTop).X - scroll.X
            : topLeft.X + 8;

        var word = new Rect(new Point(topLeft.X, topLeft.Y), new Point(Math.Max(right, topLeft.X + 1), bottom));
        return new Rect(_textView.Bounds.Size).Intersects(word) ? word : null;
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
        if (_popup.IsOpen) _dismissTimer.Start();
    }

    private void OnDismissTimerTick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();
        var overWord = _textView.IsPointerOver && _shownWord.Inflate(2).Contains(_pointer);
        if (!_isPointerOverTip && !overWord) Hide();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }

        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // VS Code's "Show Hover": Ctrl+K, then Ctrl+I.
        if (_chordStarted)
        {
            _chordStarted = false;
            if (isModifier && e.Key == Key.I)
            {
                ShowAtCaret();
                e.Handled = true;
                return;
            }
        }
        if (isModifier && e.Key == Key.K)
        {
            _chordStarted = true;
            return;
        }

        if (!_popup.IsOpen) return;
        Hide();
        if (e.Key == Key.Escape) e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => Hide();

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e) => Hide();

    private void OnScrollOffsetChanged(object? sender, EventArgs e) => Hide();

    private void OnCaretPositionChanged(object? sender, EventArgs e) => Hide();

    private void OnTextChanged(object? sender, EventArgs e)
    {
        _textVersion++;
        Hide();
    }

    private void OnTextViewDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Hide();
        if (_popup.Parent != null) ((ISetLogicalParent)_popup).SetParent(null);
    }

    private bool IsDarkTheme()
    {
        var variant = _editor.ActualThemeVariant;
        return variant == ThemeVariant.Dark ||
               (variant != ThemeVariant.Light && Application.Current?.ActualThemeVariant == ThemeVariant.Dark);
    }

    private static bool IsIdentifierCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';

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
