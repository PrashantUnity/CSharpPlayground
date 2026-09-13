using System;
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
            // 1. VS Code Dark+ Syntax Highlighting Theme
            _editor.SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetDarkTheme();

            // 2. High-contrast modern dark palette
            _editor.Background = new SolidColorBrush(Color.Parse("#14171F"));
            _editor.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            _editor.LineNumbersForeground = new SolidColorBrush(Color.Parse("#6E7681"));
            _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#264F78"));
            _editor.TextArea.SelectionForeground = null;
            _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#58A6FF"));

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

            // 7. Integrated Search & Replace Panel (Ctrl+F / Cmd+F)
            _searchPanel = SearchPanel.Install(_editor);

            // 8. Event listeners
            _editor.TextChanged += OnEditorTextChanged;
            _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            _editor.KeyDown += OnEditorKeyDown;
        }

        DataContextChanged += OnDataContextChanged;
    }

    private void PolishLeftMargins()
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
                foldingMargin.FoldingMarkerBrush = new SolidColorBrush(Color.Parse("#8B949E"));
                foldingMargin.FoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#1E2633"));
                foldingMargin.SelectedFoldingMarkerBrush = new SolidColorBrush(Color.Parse("#58A6FF"));
                foldingMargin.SelectedFoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#264F78"));
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
            _currentVm.PropertyChanged += OnVmPropertyChanged;

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
        if (_editor == null || _foldingManager == null) return;

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
