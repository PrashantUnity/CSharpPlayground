using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
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

    private bool _isSyncing;

    public BindableTextEditor()
    {
        SyntaxHighlighting = CSharpSyntaxHighlightingTheme.GetDarkTheme();
        ShowLineNumbers = true;
        WordWrap = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

        FontFamily = new FontFamily("Consolas, Menlo, Monaco, Roboto Mono, JetBrains Mono, monospace");
        FontSize = 13;

        Background = new SolidColorBrush(Color.Parse("#14171F"));
        Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
        LineNumbersForeground = new SolidColorBrush(Color.Parse("#6E7681"));

        TextArea.SelectionBrush = new SolidColorBrush(Color.Parse("#264F78"));
        TextArea.SelectionForeground = null;
        TextArea.Caret.CaretBrush = new SolidColorBrush(Color.Parse("#58A6FF"));

        TextChanged += OnEditorTextChanged;
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
        // Ctrl+Enter or Cmd+Enter to execute the cell
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
