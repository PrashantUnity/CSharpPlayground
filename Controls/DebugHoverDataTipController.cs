using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public class DebugHoverDataTipController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly DebugHoverDataTipControl _tipControl;
    private readonly Func<bool> _isPausedFunc;
    private readonly Func<IReadOnlyList<DebugVariableItem>> _getLocalsFunc;
    private readonly Func<string, Task<(bool Success, string Result, string TypeName)>>? _evaluateFunc;
    private readonly Action<string>? _addWatchAction;

    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _dismissTimer;

    private string? _pendingId;
    private string? _pendingExpr;
    private int _pendingLine;
    private int _pendingCol;

    private string? _currentHoverId;
    private int _currentHoverLine = -1;
    private bool _isPointerOverTip;

    public DebugHoverDataTipController(
        TextEditor editor,
        DebugHoverDataTipControl tipControl,
        Func<bool> isPausedFunc,
        Func<IReadOnlyList<DebugVariableItem>> getLocalsFunc,
        Func<string, Task<(bool Success, string Result, string TypeName)>>? evaluateFunc = null,
        Action<string>? addWatchAction = null)
    {
        _editor = editor;
        _tipControl = tipControl;
        _isPausedFunc = isPausedFunc;
        _getLocalsFunc = getLocalsFunc;
        _evaluateFunc = evaluateFunc;
        _addWatchAction = addWatchAction;

        _hoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _hoverTimer.Tick += OnHoverTimerTick;

        _dismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _dismissTimer.Tick += (s, e) =>
        {
            _dismissTimer.Stop();
            if (!_isPointerOverTip)
            {
                HideTip();
            }
        };

        // Hook editor pointer events
        _editor.TextArea.TextView.PointerMoved += OnTextViewPointerMoved;
        _editor.TextArea.TextView.PointerExited += OnTextViewPointerExited;
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _editor.KeyDown += OnEditorKeyDown;

        // Hook tip control events
        _tipControl.PointerEntered += (s, e) =>
        {
            _isPointerOverTip = true;
            _dismissTimer.Stop();
        };
        _tipControl.PointerExited += (s, e) =>
        {
            _isPointerOverTip = false;
            _dismissTimer.Start();
        };
        _tipControl.CloseRequested += HideTip;
        _tipControl.AddWatchRequested += expr =>
        {
            _addWatchAction?.Invoke(expr);
        };
    }

    public void HideTip()
    {
        _hoverTimer.Stop();
        _dismissTimer.Stop();
        _tipControl.IsVisible = false;
        _currentHoverId = null;
        _currentHoverLine = -1;
    }

    private void OnTextViewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPausedFunc())
        {
            if (_tipControl.IsVisible) HideTip();
            return;
        }

        if (_isPointerOverTip) return;

        var textView = _editor.TextArea.TextView;
        if (textView == null || !textView.VisualLinesValid) return;

        var pos = e.GetPosition(textView);
        var textPos = textView.GetPositionFloor(pos);

        if (!textPos.HasValue)
        {
            if (_tipControl.IsVisible)
            {
                _dismissTimer.Start();
            }
            return;
        }

        int line = textPos.Value.Line;
        int col = textPos.Value.Column;

        var doc = _editor.Document;
        if (doc == null || line <= 0 || line > doc.LineCount) return;

        var docLine = doc.GetLineByNumber(line);
        var lineText = doc.GetText(docLine.Offset, docLine.Length);

        var (id, fullExpr, startCol, endCol) = ExpressionUnderCursorFinder.FindExpression(lineText, col);

        if (string.IsNullOrEmpty(id))
        {
            if (_tipControl.IsVisible)
            {
                _dismissTimer.Start();
            }
            return;
        }

        // If already showing this variable on this line, cancel any pending dismiss
        if (_tipControl.IsVisible && _currentHoverId == id && _currentHoverLine == line)
        {
            _dismissTimer.Stop();
            return;
        }

        _pendingId = id;
        _pendingExpr = fullExpr;
        _pendingLine = line;
        _pendingCol = startCol;

        _dismissTimer.Stop();
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void OnTextViewPointerExited(object? sender, PointerEventArgs e)
    {
        if (_tipControl.IsVisible && !_isPointerOverTip)
        {
            _dismissTimer.Start();
        }
        else
        {
            _hoverTimer.Stop();
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        HideTip();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideTip();
            e.Handled = true;
        }
    }

    private async void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();

        if (!_isPausedFunc() || string.IsNullOrEmpty(_pendingId)) return;

        var locals = _getLocalsFunc();
        var id = _pendingId;
        var expr = _pendingExpr ?? id;
        var line = _pendingLine;
        var col = _pendingCol;

        // 1. Check direct local match by identifier
        var targetVar = locals.FirstOrDefault(l => string.Equals(l.Name, id, StringComparison.Ordinal));

        // 2. If not found and expr is a dotted path (e.g. environment.Application), inspect root object
        if (targetVar == null && expr.Contains('.'))
        {
            var parts = expr.Split('.');
            var root = locals.FirstOrDefault(l => string.Equals(l.Name, parts[0], StringComparison.Ordinal));
            if (root != null)
            {
                DebugVariableItem current = root;
                for (int i = 1; i < parts.Length; i++)
                {
                    var child = current.Children.FirstOrDefault(c => string.Equals(c.Name, parts[i], StringComparison.Ordinal));
                    if (child != null)
                    {
                        current = child;
                    }
                    else
                    {
                        current = null!;
                        break;
                    }
                }
                targetVar = current;
            }
        }

        // 3. If hovered over a member name (e.g. "Application"), check if any local has a child property matching this name
        if (targetVar == null)
        {
            foreach (var local in locals)
            {
                var child = local.Children.FirstOrDefault(c => string.Equals(c.Name, id, StringComparison.Ordinal));
                if (child != null)
                {
                    targetVar = child;
                    break;
                }
            }
        }

        // 4. Fallback: evaluate via Roslyn immediate service if available
        if (targetVar == null && _evaluateFunc != null)
        {
            try
            {
                var evalExpr = !string.IsNullOrEmpty(expr) ? expr : id;
                var (ok, res, type) = await _evaluateFunc(evalExpr);
                if (ok && !string.IsNullOrEmpty(res) && !res.Contains("does not exist in the current context"))
                {
                    targetVar = new DebugVariableItem
                    {
                        Name = evalExpr,
                        ValueDisplay = res,
                        TypeName = type
                    };
                }
            }
            catch
            {
                // Ignore evaluation failures for arbitrary keywords/tokens
            }
        }

        if (targetVar == null) return;

        // Position the hover tip
        var textView = _editor.TextArea.TextView;
        if (textView == null || !textView.VisualLinesValid) return;

        try
        {
            var visualPos = textView.GetVisualPosition(new TextViewPosition(line, col), VisualYPosition.LineBottom);
            var parent = _tipControl.Parent as Visual;
            var pt = parent != null ? (textView.TranslatePoint(visualPos, parent) ?? visualPos) : visualPos;

            double parentWidth = parent is Control c ? c.Bounds.Width : 800;
            double parentHeight = parent is Control ch ? ch.Bounds.Height : 600;

            // Clamp X so it doesn't overflow container width
            double x = Math.Max(12, Math.Min(pt.X, parentWidth - 360));
            double y = pt.Y + 6;

            // If too close to bottom, position above the line
            if (y + 160 > parentHeight)
            {
                var topPos = textView.GetVisualPosition(new TextViewPosition(line, col), VisualYPosition.LineTop);
                var topPt = parent != null ? (textView.TranslatePoint(topPos, parent) ?? topPos) : topPos;
                y = Math.Max(10, topPt.Y - 140);
            }

            _tipControl.SetVariable(expr, targetVar);
            _tipControl.Margin = new Thickness(x, y, 0, 0);
            _tipControl.IsVisible = true;

            _currentHoverId = id;
            _currentHoverLine = line;
        }
        catch
        {
            // Ignore layout measurement transient errors during scroll
        }
    }

    public void Dispose()
    {
        _hoverTimer.Stop();
        _dismissTimer.Stop();

        _editor.TextArea.TextView.PointerMoved -= OnTextViewPointerMoved;
        _editor.TextArea.TextView.PointerExited -= OnTextViewPointerExited;
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _editor.KeyDown -= OnEditorKeyDown;
    }
}
