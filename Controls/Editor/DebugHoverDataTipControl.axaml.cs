using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class DebugHoverDataTipControl : UserControl
{
    private string? _currentExpression;
    private DebugVariableItem? _currentVariable;

    public event Action<string>? AddWatchRequested;
    public event Action? CloseRequested;
    public event Action<DebugVariableItem>? ExploreRequested;
    public event Action<DebugVariableItem>? ViewRequested;

    public DebugHoverDataTipControl()
    {
        InitializeComponent();

        AddWatchButton.Click += OnAddWatchClicked;
        CopyButton.Click += OnCopyClicked;
        CloseButton.Click += OnCloseClicked;
        RootExploreButton.Click += OnRootExploreClicked;
        RootViewButton.Click += OnRootViewClicked;
        RootExpandToggle.IsCheckedChanged += OnRootExpandToggleChanged;
    }

    public void SetVariable(string expression, DebugVariableItem variable)
    {
        _currentExpression = expression;
        _currentVariable = variable;

        VariableNameText.Text = !string.IsNullOrEmpty(expression) ? expression : variable.Name;
        VariableTypeText.Text = $"{{{variable.TypeName}}}";
        ValueSummaryText.Text = variable.ValueDisplay;

        if (Enum.TryParse<MaterialIconKind>(variable.IconKind, out var iconKind))
        {
            RootNodeIcon.Kind = iconKind;
        }
        else
        {
            RootNodeIcon.Kind = MaterialIconKind.Variable;
        }

        RootExploreButton.IsVisible = variable.IsCollection;
        RootViewButton.IsVisible = variable.IsTextOrStructured;

        ChildrenItemsControl.ItemsSource = variable.Children;
        bool hasMembers = variable.CanExpand;
        ChildrenContainer.IsVisible = hasMembers;
        RootExpandToggle.IsVisible = hasMembers;
        RootExpandToggle.IsChecked = hasMembers;
        RootExpandIcon.Kind = hasMembers ? MaterialIconKind.ChevronDown : MaterialIconKind.ChevronRight;
    }

    private void OnRootExpandToggleChanged(object? sender, RoutedEventArgs e)
    {
        bool expanded = RootExpandToggle.IsChecked == true;
        ChildrenContainer.IsVisible = expanded;
        RootExpandIcon.Kind = expanded ? MaterialIconKind.ChevronDown : MaterialIconKind.ChevronRight;
    }

    private void OnRootExploreClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentVariable != null)
        {
            ExploreRequested?.Invoke(_currentVariable);
        }
    }

    private void OnRootViewClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentVariable != null)
        {
            ViewRequested?.Invoke(_currentVariable);
        }
    }

    private void OnChildExploreClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is DebugVariableItem item)
        {
            ExploreRequested?.Invoke(item);
        }
    }

    private void OnChildViewClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is DebugVariableItem item)
        {
            ViewRequested?.Invoke(item);
        }
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
