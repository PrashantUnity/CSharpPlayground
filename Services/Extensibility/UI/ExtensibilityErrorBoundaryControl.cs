using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;

/// <summary>
/// Avalonia ContentControl that shields the main IDE window from crashes caused by
/// dynamic user scripts or AI-generated visual components. If rendering or instantiation
/// throws, it renders an inline diagnostic badge instead of crashing the UI thread.
/// </summary>
public class ExtensibilityErrorBoundaryControl : ContentControl
{
    public static readonly StyledProperty<string?> ComponentTitleProperty =
        AvaloniaProperty.Register<ExtensibilityErrorBoundaryControl, string?>(nameof(ComponentTitle), "Custom UI Component");

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<ExtensibilityErrorBoundaryControl, bool>(nameof(HasError), false);

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<ExtensibilityErrorBoundaryControl, string?>(nameof(ErrorMessage));

    public string? ComponentTitle
    {
        get => GetValue(ComponentTitleProperty);
        set => SetValue(ComponentTitleProperty, value);
    }

    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        private set => SetValue(HasErrorProperty, value);
    }

    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        private set => SetValue(ErrorMessageProperty, value);
    }

    private Func<object>? _contentFactory;
    private object? _cachedChild;

    public ExtensibilityErrorBoundaryControl()
    {
    }

    public ExtensibilityErrorBoundaryControl(Func<object> factory, string? title = null)
    {
        ComponentTitle = title ?? "Extension Component";
        _contentFactory = factory;
        MountFactory();
    }

    public void SetFactory(Func<object> factory)
    {
        _contentFactory = factory;
        MountFactory();
    }

    public void MountFactory()
    {
        if (_contentFactory == null) return;

        try
        {
            var created = _contentFactory();
            _cachedChild = created;
            Content = created is Control ctrl ? ctrl : new ContentControl { Content = created };
            HasError = false;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            CatchError(ex);
        }
    }

    public void CatchError(Exception ex)
    {
        HasError = true;
        ErrorMessage = ex.Message;

        var errorContainer = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#201214")),
            BorderBrush = new SolidColorBrush(Color.Parse("#DC2626")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(6)
        };

        var stack = new StackPanel { Spacing = 6 };

        var headerRow = new DockPanel();
        var icon = new TextBlock
        {
            Text = "⚠",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#EF4444")),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(icon, Dock.Left);

        var titleBlock = new TextBlock
        {
            Text = $"{ComponentTitle ?? "Custom Component"} Error",
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#EF4444")),
            VerticalAlignment = VerticalAlignment.Center
        };

        var retryBtn = new Button
        {
            Content = "Retry",
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11,
            Background = new SolidColorBrush(Color.Parse("#371C1E")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(retryBtn, Dock.Right);
        retryBtn.Click += (_, _) => MountFactory();

        headerRow.Children.Add(icon);
        headerRow.Children.Add(retryBtn);
        headerRow.Children.Add(titleBlock);

        var msgBlock = new TextBlock
        {
            Text = ex.Message,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#FCA5A5")),
            TextWrapping = TextWrapping.Wrap
        };

        stack.Children.Add(headerRow);
        stack.Children.Add(msgBlock);
        errorContainer.Child = stack;

        Content = errorContainer;
    }
}
