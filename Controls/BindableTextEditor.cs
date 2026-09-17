using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using PdfEditorApp.Plugins.CSharpEditor.Services;

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
    private static readonly Lazy<RoslynCompilerService> SharedCompiler = new(() => new RoslynCompilerService());
    private readonly CSharpEditorCompletionController _completionController;
    private readonly BreakpointMargin _breakpointMargin = new();
    private readonly DebugLineRenderer _debugLineRenderer = new();

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
        ApplyThemeVariant();
        ActualThemeVariantChanged += (s, e) => ApplyThemeVariant();

        TextArea.LeftMargins.Insert(0, _breakpointMargin);
        TextArea.TextView.BackgroundRenderers.Add(_debugLineRenderer);

        _completionController = new CSharpEditorCompletionController(this, () => SharedCompiler.Value);

        TextChanged += OnEditorTextChanged;
    }

    public void SetPausedLine(int line)
    {
        _breakpointMargin.CurrentPausedLine = line;
        _debugLineRenderer.CurrentPausedLine = line;
        TextArea.TextView.InvalidateVisual();
    }

    public void ApplyThemeVariant()
    {
        bool isDark = ActualThemeVariant == ThemeVariant.Dark ||
                      (ActualThemeVariant != ThemeVariant.Light && (Application.Current?.ActualThemeVariant == ThemeVariant.Dark));

        if (isDark)
        {
            SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetDarkTheme();
            Background = new SolidColorBrush(Color.Parse("#14171F"));
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            LineNumbersForeground = new SolidColorBrush(Color.Parse("#6E7681"));
            TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#264F78"));
            TextArea.SelectionForeground = null;
            TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#58A6FF"));
        }
        else
        {
            SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetLightTheme();
            Background = new SolidColorBrush(Color.Parse("#FFFFFF"));
            Foreground = new SolidColorBrush(Color.Parse("#1E293B"));
            LineNumbersForeground = new SolidColorBrush(Color.Parse("#94A3B8"));
            TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#BFDBFE"));
            TextArea.SelectionForeground = null;
            TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#0F172A"));
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyThemeVariant();
    }

    private void UpdateCodeFolding()
    {
        if (_foldingManager != null && Document != null)
        {
            try
            {
                _foldingStrategy.UpdateFoldings(_foldingManager, Document);
            }
            catch { }
        }
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
                UpdateCodeFolding();
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
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
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F9)
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

        base.OnKeyDown(e);
    }
}
