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
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using CSharpNotebookStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel;
using NotebookCellViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookCellViewModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;

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
    private bool _isPointerInteraction;
    private readonly FoldingManager? _foldingManager;
    private readonly CSharpFoldingStrategy _foldingStrategy = new();
    private readonly DispatcherTimer _foldingTimer;
    private readonly SearchPanel? _searchPanel;
    private NotebookCellViewModel? _cellVm;
    // The cell's language (null: C#, as for an editor that isn't a notebook cell's).
    private ILanguageDefinition? _language;
    private bool _isMarkdown; // a markdown cell being edited: markdown colours, wrapped, no code helpers
    private IDisposable? _languageAssistant;
    private static readonly Lazy<RoslynCompilerService> SharedCompiler = new(() => new RoslynCompilerService());
    private static readonly Lazy<CSharpQuickInfoService> SharedQuickInfo = new(() => new CSharpQuickInfoService(SharedCompiler.Value));
    private readonly CSharpEditorCompletionController _completionController;
    private readonly CSharpQuickInfoController _quickInfoController;
    private readonly BreakpointMargin _breakpointMargin = new();
    private readonly DebugLineRenderer _debugLineRenderer = new();
    private readonly DebugLineRenderer _stepLineRenderer = new(DebugLineRenderer.VisualizerStepColor);


    private static readonly IBrush s_darkForeground = new SolidColorBrush(Color.Parse("#D4D4D4"));
    private static readonly IBrush s_darkLineNumbers = new SolidColorBrush(Color.Parse("#6E7681"));
    private static readonly IBrush s_darkSelection = new SolidColorBrush(Color.Parse("#264F78"));
    private static readonly IBrush s_darkCaret = new SolidColorBrush(Color.Parse("#58A6FF"));
    private static readonly IBrush s_darkLink = new SolidColorBrush(Color.Parse("#4FC1FF"));
    private static readonly IBrush s_darkFoldMarker = new SolidColorBrush(Color.Parse("#8B949E"));
    private static readonly IBrush s_darkFoldMarkerBg = new SolidColorBrush(Color.Parse("#1E2633"));
    private static readonly IBrush s_darkFoldActive = new SolidColorBrush(Color.Parse("#58A6FF"));
    private static readonly IBrush s_darkFoldActiveBg = new SolidColorBrush(Color.Parse("#264F78"));

    private static readonly IBrush s_lightForeground = new SolidColorBrush(Color.Parse("#1E293B"));
    private static readonly IBrush s_lightLineNumbers = new SolidColorBrush(Color.Parse("#64748B"));
    private static readonly IBrush s_lightSelection = new SolidColorBrush(Color.Parse("#ADD6FF"));
    private static readonly IBrush s_lightCaret = new SolidColorBrush(Color.Parse("#0F172A"));
    private static readonly IBrush s_lightLink = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly IBrush s_lightFoldMarker = new SolidColorBrush(Color.Parse("#64748B"));
    private static readonly IBrush s_lightFoldMarkerBg = new SolidColorBrush(Color.Parse("#F1F5F9"));
    private static readonly IBrush s_lightFoldActive = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly IBrush s_lightFoldActiveBg = new SolidColorBrush(Color.Parse("#DBEAFE"));

    /// <summary>
    /// Optional settings store; when set the editor honours EnableAutoCompletion and
    /// EnableSyntaxHighlighting. Injected by the notebook view after the editor is created.
    /// </summary>
    public Services.Settings.StudioSettingsStore? StudioSettings { get; set; }

    public BreakpointMargin BreakpointMargin => _breakpointMargin;
    public DebugLineRenderer DebugLineRenderer => _debugLineRenderer;

    public BindableTextEditor()
    {
        ShowLineNumbers = true;
        WordWrap = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

        // The layout's code font (Settings → Layout & Typography), and every change to it.
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.Tokens.SetFontFamily(this, "DsCodeFontFamily");
        FontSize = 13;

        // The current line is marked in the cell being typed in only (as in VS Code): a band in every cell was noise.
        Options.HighlightCurrentLine = false;
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
            IsSuppressed = () => !_language.UsesRoslynHelper(LanguageCapabilities.Completion)
                                 || StudioSettings?.GetSettings().EnableAutoCompletion == false
        };
        _quickInfoController = new CSharpQuickInfoController(this, () => SharedQuickInfo.Value)
        {
            PrecedingContextProvider = () => _cellVm?.GetPrecedingContext() ?? string.Empty,
            IsSuppressed = () => !_language.UsesRoslynHelper(LanguageCapabilities.QuickInfo)
        };
        TextChanged += OnEditorTextChanged;

        // Prevent oversized cell editors from snapping the parent notebook ScrollViewer to the top of the cell
        AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnEditorPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnEditorPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnEditorPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isPointerInteraction = true;
        if (_cellVm != null)
        {
            if (this.FindAncestorOfType<Views.CSharpNotebookStudioView>()?.DataContext is CSharpNotebookStudioViewModel studioVm)
            {
                studioVm.SelectCell(_cellVm);
            }
            else
            {
                _cellVm.IsSelected = true;
            }
        }
    }

    private void OnEditorPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _isPointerInteraction = false, DispatcherPriority.Input);
    }

    private void OnEditorPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _isPointerInteraction = false, DispatcherPriority.Input);
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
        ScrollPositionIntoViewIfNeeded(new TextViewPosition(line, 1), force: true);
    }

    private IBrush ResolveBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush brush)
            return brush;
        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush appBrush)
            return appBrush;
        return fallback;
    }

    public void ApplyThemeVariant()
    {
        bool isDark = ActualThemeVariant == ThemeVariant.Dark ||
                      (ActualThemeVariant != ThemeVariant.Light && (Application.Current?.ActualThemeVariant == ThemeVariant.Dark));

        bool syntaxEnabled = StudioSettings?.GetSettings().EnableSyntaxHighlighting ?? true;
        SyntaxColoring.Apply(this, _language, isDark, syntaxEnabled, _isMarkdown ? ".md" : null);

        if (isDark)
        {
            Background = Brushes.Transparent;
            Foreground = ResolveBrush("EditorFgBrush", s_darkForeground);
            LineNumbersForeground = ResolveBrush("EditorLineNumbersBrush", s_darkLineNumbers);
            TextArea.SelectionBrush = ResolveBrush("EditorSelectionBrush", s_darkSelection);
            TextArea.SelectionForeground = null;
            TextArea.Caret.CaretBrush = ResolveBrush("EditorCaretBrush", s_darkCaret);
            TextArea.TextView.LinkTextForegroundBrush = ResolveBrush("EditorLinkBrush", s_darkLink);
        }
        else
        {
            Background = Brushes.Transparent;
            Foreground = ResolveBrush("EditorFgBrush", s_lightForeground);
            LineNumbersForeground = ResolveBrush("EditorLineNumbersBrush", s_lightLineNumbers);
            TextArea.SelectionBrush = ResolveBrush("EditorSelectionBrush", s_lightSelection);
            TextArea.SelectionForeground = null;
            TextArea.Caret.CaretBrush = ResolveBrush("EditorCaretBrush", s_lightCaret);
            TextArea.TextView.LinkTextForegroundBrush = ResolveBrush("EditorLinkBrush", s_lightLink);
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
                    foldingMargin.FoldingMarkerBrush = ResolveBrush("EditorFoldingMarkerBrush", s_darkFoldMarker);
                    foldingMargin.FoldingMarkerBackgroundBrush = ResolveBrush("EditorFoldingMarkerBgBrush", s_darkFoldMarkerBg);
                    foldingMargin.SelectedFoldingMarkerBrush = ResolveBrush("EditorFoldingMarkerActiveBrush", s_darkFoldActive);
                    foldingMargin.SelectedFoldingMarkerBackgroundBrush = ResolveBrush("EditorFoldingMarkerActiveBgBrush", s_darkFoldActiveBg);
                }
                else
                {
                    foldingMargin.FoldingMarkerBrush = ResolveBrush("EditorFoldingMarkerBrush", s_lightFoldMarker);
                    foldingMargin.FoldingMarkerBackgroundBrush = ResolveBrush("EditorFoldingMarkerBgBrush", s_lightFoldMarkerBg);
                    foldingMargin.SelectedFoldingMarkerBrush = ResolveBrush("EditorFoldingMarkerActiveBrush", s_lightFoldActive);
                    foldingMargin.SelectedFoldingMarkerBackgroundBrush = ResolveBrush("EditorFoldingMarkerActiveBgBrush", s_lightFoldActiveBg);
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
        ApplyCell();
    }

    // A code cell gets its language; a markdown cell is text (no completion or folding) coloured as markdown, wrapped.
    private void ApplyCell()
    {
        if (_cellVm == null) return;
        var markdown = _cellVm.IsMarkdownCell;
        var language = markdown ? StudioLanguageServices.Default.Registry.Get(LanguageIds.Text) : _cellVm.EffectiveLanguageDefinition;
        if (markdown != _isMarkdown)
        {
            _isMarkdown = markdown;
            WordWrap = markdown;
            HorizontalScrollBarVisibility = markdown ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            if (ReferenceEquals(_language, language)) ApplyThemeVariant();
        }

        ApplyLanguage(language);
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
        else if (e.PropertyName is nameof(NotebookCellViewModel.EffectiveLanguage) or nameof(NotebookCellViewModel.Type) && _cellVm != null)
        {
            ApplyCell();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.SyntaxPaletteApplier.EditorColorsChanged -= OnEditorColorsChanged;
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
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.SyntaxPaletteApplier.EnsureSubscribed();
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.SyntaxPaletteApplier.EditorColorsChanged += OnEditorColorsChanged;
        ApplyThemeVariant();
        UpdateCodeFolding();
    }

    // Another theme (even of the same light or dark scheme): its editor and syntax colours.
    private void OnEditorColorsChanged()
    {
        ApplyThemeVariant();
        TextArea.TextView.Redraw();
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

        if (change.Property == IsKeyboardFocusWithinProperty)
        {
            Options.HighlightCurrentLine = change.GetNewValue<bool>();
            return;
        }

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
        // Never auto-scroll the outer notebook canvas on focus or bring-into-view requests.
        e.Handled = true;
    }

    public void ScrollCaretIntoViewIfNeeded()
    {
        if (_isPointerInteraction || !IsKeyboardFocusWithin) return;
        var caret = TextArea?.Caret;
        if (caret != null)
        {
            ScrollPositionIntoViewIfNeeded(caret.Position);
        }
    }

    private void ScrollPositionIntoViewIfNeeded(TextViewPosition position, bool force = false)
    {
        try
        {
            if (_isPointerInteraction) return;
            if (!force && !IsKeyboardFocusWithin) return;

            var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
            if (scrollViewer == null) return;

            var textView = TextArea?.TextView;
            if (textView == null || !textView.IsVisible) return;

            textView.EnsureVisualLines();

            // Compute visual position of the target line relative to textView
            var caretBottom = textView.GetVisualPosition(position, AvaloniaEdit.Rendering.VisualYPosition.LineBottom) - textView.ScrollOffset;
            var caretTop = textView.GetVisualPosition(position, AvaloniaEdit.Rendering.VisualYPosition.LineTop) - textView.ScrollOffset;
            var caretHeight = Math.Max(18, caretBottom.Y - caretTop.Y);

            // Translate points from textView to scrollViewer coordinates
            var pInScroll = textView.TranslatePoint(caretBottom, scrollViewer);
            if (!pInScroll.HasValue) return;

            var caretYInScroll = pInScroll.Value.Y;
            var viewportHeight = scrollViewer.Viewport.Height;
            if (viewportHeight <= 0) return;

            const double padding = 8.0;
            var currentOffset = scrollViewer.Offset;

            if (caretYInScroll > viewportHeight)
            {
                // Caret is below viewport -> scroll down just enough to reveal it
                var delta = caretYInScroll - viewportHeight + padding;
                scrollViewer.Offset = new Vector(currentOffset.X, currentOffset.Y + delta);
            }
            else if (caretYInScroll - caretHeight < 0)
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
