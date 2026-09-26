using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public class BindableTextEditor : TextEditor
{
    protected override Type StyleKeyOverride => typeof(TextEditor);

    public static readonly StyledProperty<string?> TextContentProperty =
        AvaloniaProperty.Register<BindableTextEditor, string?>(
            nameof(TextContent),
            defaultValue: string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> ExecuteCommandProperty =
        AvaloniaProperty.Register<BindableTextEditor, ICommand?>(
            nameof(ExecuteCommand));

    public string? TextContent
    {
        get => GetValue(TextContentProperty);
        set => SetValue(TextContentProperty, value);
    }

    public ICommand? ExecuteCommand
    {
        get => GetValue(ExecuteCommandProperty);
        set => SetValue(ExecuteCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> ExecuteAndNextCommandProperty =
        AvaloniaProperty.Register<BindableTextEditor, ICommand?>(
            nameof(ExecuteAndNextCommand));

    public ICommand? ExecuteAndNextCommand
    {
        get => GetValue(ExecuteAndNextCommandProperty);
        set => SetValue(ExecuteAndNextCommandProperty, value);
    }

    private bool _isSyncing;
    private readonly FoldingManager? _foldingManager;
    private readonly CSharpFoldingStrategy _foldingStrategy = new();
    private readonly DispatcherTimer _foldingTimer;
    private readonly SearchPanel? _searchPanel;
    private NotebookCellViewModel? _cellVm;
    // The cell's language (null: C#, as for an editor that isn't a notebook cell's).
    private ILanguageDefinition? _language;
    private IDisposable? _languageAssistant;
    private static readonly Lazy<RoslynCompilerService> SharedCompiler = new(() => new RoslynCompilerService());
    private static readonly Lazy<CSharpQuickInfoService> SharedQuickInfo = new(() => new CSharpQuickInfoService(SharedCompiler.Value));
    private readonly CSharpEditorCompletionController _completionController;
    private readonly CSharpQuickInfoController _quickInfoController;
    private readonly BreakpointMargin _breakpointMargin = new();
    private readonly DebugLineRenderer _debugLineRenderer = new();
    private readonly DebugLineRenderer _stepLineRenderer = new(DebugLineRenderer.VisualizerStepColor);

    public BreakpointMargin BreakpointMargin => _breakpointMargin;
    public DebugLineRenderer DebugLineRenderer => _debugLineRenderer;

    public BindableTextEditor()
    {
        ShowLineNumbers = true;
        WordWrap = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

        FontFamily = new FontFamily("JetBrains Mono, Menlo, Monaco, Consolas, Roboto Mono, monospace");
        FontSize = 13;

        Options.HighlightCurrentLine = true;
        Options.ConvertTabsToSpaces = true;
        Options.IndentationSize = 4;
        TextArea.IndentationStrategy = new AvaloniaEdit.Indentation.CSharp.CSharpIndentationStrategy(Options);

        _foldingManager = AvaloniaEdit.Folding.FoldingManager.Install(TextArea);
        _foldingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _foldingTimer.Tick += (s, e) =>
        {
            _foldingTimer.Stop();
            UpdateCodeFolding();
        };

        _searchPanel = SearchPanel.Install(this);

        ApplyThemeVariant();
        ActualThemeVariantChanged += (s, e) => ApplyThemeVariant();

        TextArea.LeftMargins.Insert(0, _breakpointMargin);
        TextArea.TextView.BackgroundRenderers.Add(_stepLineRenderer);
        TextArea.TextView.BackgroundRenderers.Add(_debugLineRenderer);

        // C# completion and hover, for cells whose language has them (Roslyn would only misread a Python cell).
        _completionController = new CSharpEditorCompletionController(this, () => SharedCompiler.Value)
        {
            PrecedingContextProvider = () => _cellVm?.GetPrecedingContext() ?? string.Empty,
            IsSuppressed = () => !Supports(LanguageCapabilities.Completion)
        };
        _quickInfoController = new CSharpQuickInfoController(this, () => SharedQuickInfo.Value)
        {
            PrecedingContextProvider = () => _cellVm?.GetPrecedingContext() ?? string.Empty,
            IsSuppressed = () => !Supports(LanguageCapabilities.QuickInfo)
        };

        TextChanged += OnEditorTextChanged;

        // Prevent oversized cell editors from snapping the parent notebook ScrollViewer to the top of the cell
        AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public void SetPausedLine(int line)
    {
        _breakpointMargin.CurrentPausedLine = line;
        _debugLineRenderer.HighlightedLine = line;
        TextArea.TextView.InvalidateVisual();
    }

    public void SetStepLine(int line)
    {
        if (_stepLineRenderer.HighlightedLine == line) return;
        _stepLineRenderer.HighlightedLine = line;
        TextArea.TextView.InvalidateVisual();
    }

    public void RevealLine(int line)
    {
        if (Document == null || line < 1 || line > Document.LineCount) return;
        ScrollPositionIntoViewIfNeeded(new TextViewPosition(line, 1));
    }

    public void ApplyThemeVariant()
    {
        bool isDark = ActualThemeVariant == ThemeVariant.Dark ||
                      (ActualThemeVariant != ThemeVariant.Light && (Application.Current?.ActualThemeVariant == ThemeVariant.Dark));

        if (isDark)
        {
            SyntaxHighlighting = _language != null ? _language.GetHighlighting(isDark: true) : CSharpSyntaxHighlightingTheme.GetDarkTheme();
            Background = Brushes.Transparent;
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            LineNumbersForeground = new SolidColorBrush(Color.Parse("#6E7681"));
            TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#264F78"));
            TextArea.SelectionForeground = null;
            TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#58A6FF"));
            TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush(Color.Parse("#4FC1FF"));
        }
        else
        {
            SyntaxHighlighting = _language != null ? _language.GetHighlighting(isDark: false) : CSharpSyntaxHighlightingTheme.GetLightTheme();
            Background = Brushes.Transparent;
            Foreground = new SolidColorBrush(Color.Parse("#1E293B"));
            LineNumbersForeground = new SolidColorBrush(Color.Parse("#94A3B8"));
            TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#BFDBFE"));
            TextArea.SelectionForeground = null;
            TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#0F172A"));
            TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush(Color.Parse("#2563EB"));
        }

        PolishLeftMargins(isDark);
    }

    private void PolishLeftMargins(bool isDark)
    {
        for (int i = TextArea.LeftMargins.Count - 1; i >= 0; i--)
        {
            var margin = TextArea.LeftMargins[i];
            if (margin.GetType().Name.Contains("DottedLineMargin"))
            {
                TextArea.LeftMargins.RemoveAt(i);
            }
            else if (margin is AvaloniaEdit.Folding.FoldingMargin foldingMargin)
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

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UnsubscribeCellVm();
        SubscribeCellVm();
        if (_cellVm == null) ApplyLanguage(null);
    }

    private void SubscribeCellVm()
    {
        if (_cellVm != null || DataContext is not NotebookCellViewModel vm) return;
        _cellVm = vm;
        _cellVm.RequestFoldAllCode += FoldAll;
        _cellVm.RequestUnfoldAllCode += UnfoldAll;
        _cellVm.RequestFormatCode += FormatCode;
        _cellVm.PropertyChanged += OnCellPropertyChanged;
        ApplyLanguage(vm.EffectiveLanguageDefinition);
    }

    private bool Supports(LanguageCapabilities capability) => _language?.Has(capability) ?? true;

    /// <summary>Makes the editor fit the cell's language: highlighting, indentation, folding, and the C# helpers only for C#.</summary>
    private void ApplyLanguage(ILanguageDefinition? language)
    {
        if (ReferenceEquals(_language, language)) return;
        _language = language;

        TextArea.IndentationStrategy = language?.CreateIndentationStrategy(Options)
                                       ?? new AvaloniaEdit.Indentation.CSharp.CSharpIndentationStrategy(Options);
        _breakpointMargin.IsVisible = Supports(LanguageCapabilities.Breakpoints);
        _completionController.Close();
        _quickInfoController.Hide();
        _languageAssistant?.Dispose();
        _languageAssistant = language?.EditorAssistants?.Attach(this, new EditorAssistantContext(() => _cellVm?.GetPrecedingContext() ?? string.Empty));

        ApplyThemeVariant();
        UpdateCodeFolding();
    }

    private void UnsubscribeCellVm()
    {
        if (_cellVm != null)
        {
            _cellVm.RequestFoldAllCode -= FoldAll;
            _cellVm.RequestUnfoldAllCode -= UnfoldAll;
            _cellVm.RequestFormatCode -= FormatCode;
            _cellVm.PropertyChanged -= OnCellPropertyChanged;
            _cellVm = null;
        }
    }

    private void OnCellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // A re-run replaces the cell's visualizers, so the old step line no longer applies.
        if (e.PropertyName == nameof(NotebookCellViewModel.IsExecuting) && _cellVm?.IsExecuting == true)
        {
            SetStepLine(-1);
        }
        else if (e.PropertyName == nameof(NotebookCellViewModel.EffectiveLanguage) && _cellVm != null)
        {
            ApplyLanguage(_cellVm.EffectiveLanguageDefinition);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnsubscribeCellVm();
        _languageAssistant?.Dispose();
        _languageAssistant = null;
        _language = null;
        _foldingTimer?.Stop();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeCellVm();
        ApplyThemeVariant();
        UpdateCodeFolding();
    }

    public void UpdateCodeFolding()
    {
        if (_foldingManager != null && Document != null)
        {
            try
            {
                if (_language == null)
                {
                    _foldingStrategy.UpdateFoldings(_foldingManager, Document);
                }
                else if (_language.Folding is { } folding)
                {
                    var foldings = folding.CreateFoldings(Document, out var firstErrorOffset);
                    _foldingManager.UpdateFoldings(foldings, firstErrorOffset);
                }
                else
                {
                    _foldingManager.Clear();
                }
            }
            catch { }
        }
    }

    public void ToggleFoldAtCaret(bool? fold = null)
    {
        if (_foldingManager == null) return;
        int offset = CaretOffset;
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

    public void FormatCode()
    {
        // Formatting is Roslyn's, so only for a language that has it (C#).
        if (string.IsNullOrWhiteSpace(Text) || !Supports(LanguageCapabilities.Formatting)) return;
        try
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(Text);
            var root = tree.GetRoot();
            var formatted = Microsoft.CodeAnalysis.SyntaxNodeExtensions.NormalizeWhitespace(root).ToFullString();
            if (formatted != Text)
            {
                Text = formatted;
                TextContent = formatted;
                UpdateCodeFolding();
            }
        }
        catch { }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (!string.IsNullOrEmpty(TextContent) && Text != TextContent)
        {
            _isSyncing = true;
            try
            {
                Text = TextContent;
                UpdateCodeFolding();
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (!string.IsNullOrEmpty(TextContent) && Text != TextContent)
        {
            _isSyncing = true;
            try
            {
                Text = TextContent;
            }
            finally
            {
                _isSyncing = false;
            }
        }
        UpdateCodeFolding();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        SetStepLine(-1);
        if (_isSyncing) return;

        _isSyncing = true;
        try
        {
            TextContent = Text;
        }
        finally
        {
            _isSyncing = false;
        }

        _foldingTimer.Stop();
        _foldingTimer.Start();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextContentProperty)
        {
            if (_isSyncing) return;

            _isSyncing = true;
            try
            {
                var newText = change.GetNewValue<string?>() ?? string.Empty;
                if (Text != newText)
                {
                    Text = newText;
                }
                UpdateCodeFolding();
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F9 && Supports(LanguageCapabilities.Breakpoints))
        {
            var line = TextArea.Caret.Line;
            if (_breakpointMargin.HasBreakpoint(line))
            {
                _breakpointMargin.RemoveBreakpoint(line);
            }
            else
            {
                _breakpointMargin.AddBreakpoint(line);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (ExecuteAndNextCommand != null && ExecuteAndNextCommand.CanExecute(null))
            {
                ExecuteAndNextCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            if (ExecuteCommand != null && ExecuteCommand.CanExecute(null))
            {
                ExecuteCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        var isCmdOrCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // Ctrl+Shift+[ => fold code block at caret
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemOpenBrackets || e.Key == Key.Oem4))
        {
            ToggleFoldAtCaret(fold: true);
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+] => unfold code block at caret
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemCloseBrackets || e.Key == Key.Oem6))
        {
            ToggleFoldAtCaret(fold: false);
            e.Handled = true;
            return;
        }

        // Ctrl+F => search panel
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.F)
        {
            _searchPanel?.Open();
            e.Handled = true;
            return;
        }

        // Ctrl+Alt+L or Ctrl+Shift+I => format code
        if (isCmdOrCtrl && (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.L ||
                           (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.I)))
        {
            FormatCode();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        // Stop default BringIntoView from bubbling to the outer ScrollViewer.
        // When cell content exceeds viewport height, default ScrollViewer bring-into-view
        // forces newOffset.Y = rect.Top, causing the notebook to violently snap back to the cell top.
        e.Handled = true;

        // Instead, perform smooth caret-only visibility checks:
        ScrollCaretIntoViewIfNeeded();
    }

    public void ScrollCaretIntoViewIfNeeded()
    {
        var caret = TextArea?.Caret;
        if (caret != null)
        {
            ScrollPositionIntoViewIfNeeded(caret.Position);
        }
    }

    private void ScrollPositionIntoViewIfNeeded(TextViewPosition position)
    {
        try
        {
            var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
            if (scrollViewer == null) return;

            var textView = TextArea?.TextView;
            if (textView == null || !textView.IsVisible) return;

            // Compute visual position of the target line
            var caretBottom = textView.GetVisualPosition(position, AvaloniaEdit.Rendering.VisualYPosition.LineBottom);
            var caretTop = textView.GetVisualPosition(position, AvaloniaEdit.Rendering.VisualYPosition.LineTop);
            var caretHeight = Math.Max(18, caretBottom.Y - caretTop.Y);

            // Translate points to scrollViewer coordinates
            var pInScroll = this.TranslatePoint(caretBottom, scrollViewer);
            if (!pInScroll.HasValue) return;

            var caretYInScroll = pInScroll.Value.Y;
            var viewportHeight = scrollViewer.Viewport.Height;
            if (viewportHeight <= 0) return;

            const double padding = 28.0;
            var currentOffset = scrollViewer.Offset;

            if (caretYInScroll > viewportHeight - padding)
            {
                // Caret is below viewport -> scroll down just enough to reveal it
                var delta = caretYInScroll - (viewportHeight - padding);
                scrollViewer.Offset = new Vector(currentOffset.X, currentOffset.Y + delta);
            }
            else if (caretYInScroll - caretHeight < padding)
            {
                // Caret is above viewport -> scroll up just enough to reveal it
                var delta = (caretYInScroll - caretHeight) - padding;
                scrollViewer.Offset = new Vector(currentOffset.X, Math.Max(0, currentOffset.Y + delta));
            }
        }
        catch
        {
            // Defensive guard against layout passes during rapid typing
        }
    }
}
