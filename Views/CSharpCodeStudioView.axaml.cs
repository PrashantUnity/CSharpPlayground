using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Indentation.CSharp;
using AvaloniaEdit.Search;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpCodeStudioView : UserControl
{
    private TextEditor? _editor;
    private FoldingManager? _foldingManager;
    private SearchPanel? _searchPanel;
    private CSharpEditorCompletionController? _completionController;
    private readonly CSharpFoldingStrategy _foldingStrategy = new();
    private readonly DispatcherTimer _foldingTimer;
    private CSharpCodeStudioViewModel? _currentVm;
    private bool _isUpdatingText;

    private readonly BreakpointMargin _breakpointMargin = new();
    private readonly DebugLineRenderer _debugLineRenderer = new();
    private DebugHoverDataTipControl? _debugHoverTip;
    private DebugHoverDataTipController? _debugHoverController;

    public CSharpCodeStudioView()
    {
        InitializeComponent();

        _foldingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _foldingTimer.Tick += (s, e) =>
        {
            _foldingTimer.Stop();
            UpdateCodeFolding();
        };

        _editor = this.FindControl<TextEditor>("Editor");
        if (_editor != null)
        {
            ApplyThemeVariant();
            ActualThemeVariantChanged += (s, e) => ApplyThemeVariant();

            // 3. Editor Options: Active line highlight, tab indentation
            _editor.Options.HighlightCurrentLine = true;
            _editor.Options.ConvertTabsToSpaces = true;
            _editor.Options.IndentationSize = 4;

            // 4. C# Smart Indentation
            _editor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(_editor.Options);

            // 5. Code Folding Manager (Collapsible blocks, #region, comments, etc.)
            _foldingManager = FoldingManager.Install(_editor.TextArea);

            // 6. Clean Gutter Margins: Remove ugly DottedLineMargin and style FoldingMargin
            PolishLeftMargins();

            // 7. Install Breakpoint Gutter Margin & Debug Highlight Renderer
            _editor.TextArea.LeftMargins.Insert(0, _breakpointMargin);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_debugLineRenderer);
            _breakpointMargin.BreakpointToggled += line => _currentVm?.ToggleBreakpoint(line);

            // 8. Integrated Search & Replace Panel (Ctrl+F / Cmd+F)
            _searchPanel = SearchPanel.Install(_editor);

            // 9. Interactive Live Debug Hover Data Tip Controller
            _debugHoverTip = this.FindControl<DebugHoverDataTipControl>("DebugHoverTip");
            if (_debugHoverTip != null)
            {
                _debugHoverController = new DebugHoverDataTipController(
                    _editor,
                    _debugHoverTip,
                    () => _currentVm?.IsPaused == true,
                    () => _currentVm?.Locals != null ? (IReadOnlyList<DebugVariableItem>)_currentVm.Locals : Array.Empty<DebugVariableItem>(),
                    expr => _currentVm != null ? _currentVm.EvaluateExpressionAsync(expr) : Task.FromResult((false, "", "")),
                    expr => _ = _currentVm?.AddWatchExpressionAsync(expr));
            }

            // 10. Event listeners
            _editor.TextChanged += OnEditorTextChanged;
            _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            _editor.KeyDown += OnEditorKeyDown;
        }

        DataContextChanged += OnDataContextChanged;
    }

    public void ApplyThemeVariant()
    {
        if (_editor == null) return;

        bool isDark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ||
                      (ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light && (Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark));

        if (isDark)
        {
            _editor.SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetDarkTheme();
            _editor.Background = new SolidColorBrush(Color.Parse("#14171F"));
            _editor.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            _editor.LineNumbersForeground = new SolidColorBrush(Color.Parse("#6E7681"));
            _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#264F78"));
            _editor.TextArea.SelectionForeground = null;
            _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#58A6FF"));
        }
        else
        {
            _editor.SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetLightTheme();
            _editor.Background = new SolidColorBrush(Color.Parse("#FFFFFF"));
            _editor.Foreground = new SolidColorBrush(Color.Parse("#1E293B"));
            _editor.LineNumbersForeground = new SolidColorBrush(Color.Parse("#94A3B8"));
            _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#BFDBFE"));
            _editor.TextArea.SelectionForeground = null;
            _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#0F172A"));
        }

        PolishLeftMargins(isDark);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyThemeVariant();
    }

    private void PolishLeftMargins(bool isDark = true)
    {
        if (_editor == null) return;

        // Eliminate DottedLineMargin to remove the awkward dotted vertical gutter line
        for (int i = _editor.TextArea.LeftMargins.Count - 1; i >= 0; i--)
        {
            var margin = _editor.TextArea.LeftMargins[i];
            if (margin.GetType().Name.Contains("DottedLineMargin"))
            {
                _editor.TextArea.LeftMargins.RemoveAt(i);
            }
            else if (margin is FoldingMargin foldingMargin)
            {
                if (isDark)
                {
                    foldingMargin.FoldingMarkerBrush = new SolidColorBrush(Color.Parse("#8B949E"));
                    foldingMargin.FoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#1E2633"));
                    foldingMargin.SelectedFoldingMarkerBrush = new SolidColorBrush(Color.Parse("#58A6FF"));
                    foldingMargin.SelectedFoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#264F78"));
                }
                else
                {
                    foldingMargin.FoldingMarkerBrush = new SolidColorBrush(Color.Parse("#64748B"));
                    foldingMargin.FoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#F1F5F9"));
                    foldingMargin.SelectedFoldingMarkerBrush = new SolidColorBrush(Color.Parse("#2563EB"));
                    foldingMargin.SelectedFoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#DBEAFE"));
                }
            }
        }
    }

    private void UpdateCodeFolding()
    {
        if (_foldingManager != null && _editor?.Document != null)
        {
            try
            {
                _foldingStrategy.UpdateFoldings(_foldingManager, _editor.Document);
            }
            catch
            {
                // Ignore transient syntax errors while actively editing
            }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
        {
            _currentVm.RequestNavigateToCaret -= OnNavigateToCaret;
            _currentVm.RequestFoldAll -= FoldAll;
            _currentVm.RequestUnfoldAll -= UnfoldAll;
            _currentVm.RequestToggleSearch -= ToggleSearch;
            _currentVm.RequestSetPausedLine -= OnSetPausedLine;
            _currentVm.RequestSyncBreakpoints -= OnSyncBreakpoints;
            _currentVm.PropertyChanged -= OnVmPropertyChanged;
            _completionController?.Dispose();
            _completionController = null;
        }

        _currentVm = DataContext as CSharpCodeStudioViewModel;

        if (_currentVm != null && _editor != null)
        {
            _currentVm.RequestNavigateToCaret += OnNavigateToCaret;
            _currentVm.RequestFoldAll += FoldAll;
            _currentVm.RequestUnfoldAll += UnfoldAll;
            _currentVm.RequestToggleSearch += ToggleSearch;
            _currentVm.RequestSetPausedLine += OnSetPausedLine;
            _currentVm.RequestSyncBreakpoints += OnSyncBreakpoints;
            _currentVm.PropertyChanged += OnVmPropertyChanged;

            _breakpointMargin.SetBreakpoints(_currentVm.Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));

            _completionController = new CSharpEditorCompletionController(_editor, _currentVm.CompilerService)
            {
                LanguageMode = _currentVm.CurrentLanguageMode
            };

            _editor.WordWrap = _currentVm.IsWordWrap;

            _isUpdatingText = true;
            try
            {
                _editor.Text = _currentVm.Code ?? string.Empty;
                UpdateCodeFolding();
            }
            finally
            {
                _isUpdatingText = false;
            }
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_editor == null || _currentVm == null) return;

        if (e.PropertyName == nameof(CSharpCodeStudioViewModel.IsWordWrap))
        {
            _editor.WordWrap = _currentVm.IsWordWrap;
        }
        else if (e.PropertyName == nameof(CSharpCodeStudioViewModel.SelectedLanguageModeIndex))
        {
            if (_completionController != null)
            {
                _completionController.LanguageMode = _currentVm.CurrentLanguageMode;
            }
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingText || _editor == null || _currentVm == null) return;

        _currentVm.Code = _editor.Text;

        // Debounce code folding update
        _foldingTimer.Stop();
        _foldingTimer.Start();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_editor == null || _currentVm == null) return;

        var caret = _editor.TextArea.Caret;
        _currentVm.SetCaretPosition(caret.Line, caret.Column);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor == null) return;

        // F9: Toggle breakpoint on caret line
        if (e.Key == Key.F9)
        {
            _currentVm?.ToggleBreakpoint(_editor.TextArea.Caret.Line);
            e.Handled = true;
            return;
        }

        // F5: Debug or Continue
        if (e.Key == Key.F5 && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (_currentVm?.IsPaused == true)
            {
                _currentVm.ContinueDebug();
                e.Handled = true;
                return;
            }
            else if (_currentVm?.IsExecuting == false && _currentVm?.IsDebugging == false)
            {
                _ = _currentVm.DebugCodeCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }
        }

        // F10: Step Over
        if (e.Key == Key.F10 && _currentVm?.IsPaused == true)
        {
            _currentVm.StepOver();
            e.Handled = true;
            return;
        }

        // F11: Step Into
        if (e.Key == Key.F11 && _currentVm?.IsPaused == true)
        {
            _currentVm.StepInto();
            e.Handled = true;
            return;
        }

        // Shift+F5: Stop Debugging
        if (e.Key == Key.F5 && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _currentVm?.IsDebugging == true)
        {
            _currentVm.StopDebug();
            e.Handled = true;
            return;
        }

        if (_foldingManager == null) return;

        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // Ctrl+Shift+[ to fold block at caret
        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemOpenBrackets || e.Key == Key.Oem4))
        {
            ToggleFoldAtCaret(fold: true);
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+] to unfold block at caret
        if (isModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemCloseBrackets || e.Key == Key.Oem6))
        {
            ToggleFoldAtCaret(fold: false);
            e.Handled = true;
            return;
        }
    }

    private void OnSetPausedLine(int line)
    {
        if (_editor == null) return;
        if (line == -1)
        {
            _debugHoverController?.HideTip();
        }
        _breakpointMargin.CurrentPausedLine = line;
        _debugLineRenderer.CurrentPausedLine = line;
        _editor.TextArea.TextView.InvalidateVisual();
    }

    private void OnSyncBreakpoints(IEnumerable<int> lines)
    {
        _breakpointMargin.SetBreakpoints(lines);
    }

    private void ToggleFoldAtCaret(bool? fold = null)
    {
        if (_editor == null || _foldingManager == null) return;
        int offset = _editor.CaretOffset;
        var foldings = _foldingManager.GetFoldingsContaining(offset);
        var target = foldings.OrderByDescending(f => f.StartOffset).FirstOrDefault();
        if (target != null)
        {
            target.IsFolded = fold ?? !target.IsFolded;
        }
    }

    public void FoldAll()
    {
        if (_foldingManager == null) return;
        foreach (var fold in _foldingManager.AllFoldings)
        {
            fold.IsFolded = true;
        }
    }

    public void UnfoldAll()
    {
        if (_foldingManager == null) return;
        foreach (var fold in _foldingManager.AllFoldings)
        {
            fold.IsFolded = false;
        }
    }

    public void ToggleSearch()
    {
        _searchPanel?.Open();
    }

    private void OnNavigateToCaret(int line, int col)
    {
        if (_editor == null) return;

        try
        {
            _editor.TextArea.Caret.Line = line;
            _editor.TextArea.Caret.Column = col;
            _editor.ScrollTo(line, col);
            _editor.Focus();
        }
        catch
        {
            // Ignore invalid line index if text modified
        }
    }
}
