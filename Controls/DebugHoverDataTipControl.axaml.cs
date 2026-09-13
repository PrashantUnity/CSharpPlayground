using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class DebugHoverDataTipControl : UserControl
{
    private string? _currentExpression;
    private DebugVariableItem? _currentVariable;

    public event Action<string>? AddWatchRequested;
    public event Action? CloseRequested;

    public DebugHoverDataTipControl()
    {
        InitializeComponent();

        AddWatchButton.Click += OnAddWatchClicked;
        CopyButton.Click += OnCopyClicked;
        CloseButton.Click += OnCloseClicked;
    }

    public void SetVariable(string expression, DebugVariableItem variable)
    {
        _currentExpression = expression;
        _currentVariable = variable;

        VariableNameText.Text = !string.IsNullOrEmpty(expression) ? expression : variable.Name;
        VariableTypeText.Text = $": {variable.TypeName}";
        ValueDisplayTextBox.Text = variable.ValueDisplay;

        ChildrenItemsControl.ItemsSource = variable.Children;
        ChildrenContainer.IsVisible = variable.Children.Count > 0;
        ChildrenHeader.Text = $"PROPERTIES & MEMBERS ({variable.Children.Count})";
    }

    private void OnAddWatchClicked(object? sender, RoutedEventArgs e)
    {
        var expr = _currentExpression ?? _currentVariable?.Name;
        if (!string.IsNullOrWhiteSpace(expr))
        {
            AddWatchRequested?.Invoke(expr);
        }
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentVariable != null)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(_currentVariable.ValueDisplay);
            }
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }
}
