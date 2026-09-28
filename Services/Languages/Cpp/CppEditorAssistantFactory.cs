using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Attaches hover Quick Info to C++ editors. On pointer-hover over a word, or Ctrl+K Ctrl+I
/// at the caret, it looks up the symbol in <see cref="CppQuickInfoProvider"/> and shows the
/// same <see cref="QuickInfoTipControl"/> card that C# uses — zero new UI, same VS Code feel.
/// </summary>
public sealed class CppEditorAssistantFactory : IEditorAssistantFactory
{
    /// <summary>Singleton — the factory holds no mutable state.</summary>
    public static readonly CppEditorAssistantFactory Instance = new();

    private readonly CppCompletionService _completionService = new();

    public IDisposable Attach(TextEditor editor, EditorAssistantContext context)
    {
        var quickInfo = new CppQuickInfoController(editor, context);
        var completion = new LanguageCompletionController(
            editor,
            context,
            _completionService,
            isImmediateTrigger: (ch, text, offset) =>
                ch == '.' ||
                (ch == '>' && offset > 1 && text[offset - 2] == '-') ||
                (ch == ':' && offset > 1 && text[offset - 2] == ':'));

        return new CompositeEditorAssistant(quickInfo, completion);
    }
}

/// <summary>
/// Mirrors <c>CSharpQuickInfoController</c> but resolves symbols through
/// <see cref="CppQuickInfoProvider"/> instead of Roslyn. The popup placement,
/// dismiss logic, chord handling and the <see cref="QuickInfoTipControl"/> card
/// are all identical to the C# implementation.
/// </summary>
public sealed class CppQuickInfoController : IDisposable
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

    public CppQuickInfoController(TextEditor editor, EditorAssistantContext context)
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

    /// <summary>Shows the card for the symbol at (or just before) the caret (Ctrl+K Ctrl+I).</summary>
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

    // ── Pointer events ─────────────────────────────────────────────────────

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
        if (_popup.IsOpen) _dismissTimer.Start();
    }

    private void OnDismissTimerTick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();
        if (!_isPointerOverTip && !_shownWord.Inflate(2).Contains(_pointer)) Hide();
    }

    // ── Keyboard events ────────────────────────────────────────────────────

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;

        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // VS Code chord: Ctrl+K then Ctrl+I → Show Hover at caret.
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
    private void OnTextChanged(object? sender, EventArgs e) { _textVersion++; Hide(); }

    private void OnTextViewDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Hide();
        if (_popup.Parent != null) ((ISetLogicalParent)_popup).SetParent(null);
    }

    // ── Core lookup & display ──────────────────────────────────────────────

    private void Request(int offset, bool fromPointer)
    {
        _requestCts?.Cancel();
        var cts = _requestCts = new CancellationTokenSource();
        var token = cts.Token;

        var text = _editor.Text ?? string.Empty;
        var version = _textVersion;

        // Dictionary lookup is synchronous and fast; wrap in Task.Run to stay off the UI thread
        // consistently with the C# Quick Info controller pattern.
        _ = Task.Run(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var symbol = CppQuickInfoProvider.ExtractSymbol(text, offset);
                if (symbol == null || token.IsCancellationRequested) return;

                var info = CppQuickInfoProvider.Lookup(symbol);
                if (info == null || token.IsCancellationRequested) return;

                var bare = symbol.StartsWith("std::", StringComparison.Ordinal) ? symbol[5..]
                    : symbol.StartsWith("fry::", StringComparison.Ordinal) ? symbol[5..] : symbol;

                // Find word span in the original text.
                int spanStart = offset;
                while (spanStart > 0 && IsIdentChar(text[spanStart - 1])) spanStart--;

                var csInfo = BuildCSharpQuickInfo(info, spanStart, bare.Length);

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
                Debug.WriteLine($"[CppQuickInfoController] lookup failed: {ex}");
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
        var variant = _editor.ActualThemeVariant;
        return variant == ThemeVariant.Dark ||
               (variant != ThemeVariant.Light && Application.Current?.ActualThemeVariant == ThemeVariant.Dark);
    }

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Converts a <see cref="CppQuickInfoProvider.QuickInfo"/> record into the <see cref="CSharpQuickInfo"/>
    /// model that <see cref="QuickInfoTipControl.SetQuickInfo"/> understands.
    /// </summary>
    private static CSharpQuickInfo BuildCSharpQuickInfo(CppQuickInfoProvider.QuickInfo info, int spanStart, int spanLength)
    {
        // ── Signature ─────────────────────────────────────────────────────
        var sig = new List<QuickInfoTextRun>
        {
            new(info.Signature, QuickInfoTextKind.Code)
        };

        // ── Container: header + since badge ──────────────────────────────
        var container = new List<QuickInfoTextRun>();
        if (info.Header != null)
        {
            container.Add(new("#include ", QuickInfoTextKind.Keyword));
            container.Add(new(info.Header, QuickInfoTextKind.Type));
        }
        if (info.Since != null)
        {
            if (container.Count > 0) container.Add(new("  ·  ", QuickInfoTextKind.Text));
            container.Add(new(info.Since, QuickInfoTextKind.Member));
        }

        // ── Summary ───────────────────────────────────────────────────────
        var summary = new List<QuickInfoTextRun>
        {
            new(info.Summary, QuickInfoTextKind.Text)
        };

        return new CSharpQuickInfo
        {
            SpanStart = spanStart,
            SpanLength = spanLength,
            Signature = sig,
            Container = container,
            Documentation = new QuickInfoDocumentation { Summary = summary }
        };
    }

    // ── IDisposable ───────────────────────────────────────────────────────

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
