using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Indentation.CSharp;
using AvaloniaEdit.Search;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpEditorView : UserControl
{
    private TextEditor? _editor;
    private FoldingManager? _foldingManager;
    private readonly CSharpFoldingStrategy _foldingStrategy = new();
    private readonly DispatcherTimer _foldingTimer;
    private CSharpEditorViewModel? _currentVm;
    private bool _isUpdatingText;

    public CSharpEditorView()
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
            _editor.SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetDarkTheme();

            _editor.Background = new SolidColorBrush(Color.Parse("#14171F"));
            _editor.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            _editor.LineNumbersForeground = new SolidColorBrush(Color.Parse("#6E7681"));
            _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#264F78"));
            _editor.TextArea.SelectionForeground = null;
            _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#58A6FF"));

            _editor.Options.HighlightCurrentLine = true;
            _editor.Options.ConvertTabsToSpaces = true;
            _editor.Options.IndentationSize = 4;

            _editor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(_editor.Options);
            _foldingManager = FoldingManager.Install(_editor.TextArea);
            PolishLeftMargins();

            SearchPanel.Install(_editor);

            _editor.TextChanged += OnEditorTextChanged;
            _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        }

        DataContextChanged += OnDataContextChanged;
    }

    private void PolishLeftMargins()
    {
        if (_editor == null) return;

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
            catch { }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
        {
            _currentVm.RequestNavigateToCaret -= OnNavigateToCaret;
        }

        _currentVm = DataContext as CSharpEditorViewModel;

        if (_currentVm != null && _editor != null)
        {
            _currentVm.RequestNavigateToCaret += OnNavigateToCaret;

            _isUpdatingText = true;
            try
            {
                _editor.Text = _currentVm.SourceCode ?? string.Empty;
                UpdateCodeFolding();
            }
            finally
            {
                _isUpdatingText = false;
            }
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingText || _editor == null || _currentVm == null) return;

        _currentVm.SourceCode = _editor.Text;

        _foldingTimer.Stop();
        _foldingTimer.Start();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_editor == null || _currentVm == null) return;

        var caret = _editor.TextArea.Caret;
        _currentVm.SetCaretPosition(caret.Line, caret.Column);
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
