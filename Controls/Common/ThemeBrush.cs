using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Theme brushes whose key comes from the data: <c>common:ThemeBrush.Foreground="{Binding ValueForegroundKey}"</c>,
/// likewise <c>Background</c> and <c>BorderBrush</c>. They work like <c>{DynamicResource}</c>: the brush is looked up
/// from the control, so the studio's palette and theme layer are found wherever the studio is hosted (they sit on the
/// studio's own view in FryPDF, not on the application), and it follows every theme switch. A key that doesn't resolve
/// leaves the property unset (inherited, for the foreground); a binding or <c>{DynamicResource}</c> to a missing key
/// gives the default instead, black text, unreadable on a dark theme.
/// </summary>
public sealed class ThemeBrush
{
    public static readonly AttachedProperty<string?> ForegroundProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, string?>("Foreground");

    public static readonly AttachedProperty<string?> BackgroundProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, string?>("Background");

    public static readonly AttachedProperty<string?> BorderBrushProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, string?>("BorderBrush");

    // The resource subscription each key made, so a new key replaces it.
    private static readonly AttachedProperty<IDisposable?> ForegroundSubscriptionProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, IDisposable?>("ForegroundSubscription");

    private static readonly AttachedProperty<IDisposable?> BackgroundSubscriptionProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, IDisposable?>("BackgroundSubscription");

    private static readonly AttachedProperty<IDisposable?> BorderBrushSubscriptionProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, IDisposable?>("BorderBrushSubscription");

    static ThemeBrush()
    {
        // TextBlock, MaterialIcon and templated controls share these properties (AddOwner), as do Border and Panel.
        ForegroundProperty.Changed.AddClassHandler<Control>((c, e) => OnKeyChanged(c, e, TextElement.ForegroundProperty, ForegroundSubscriptionProperty));
        BackgroundProperty.Changed.AddClassHandler<Control>((c, e) => OnKeyChanged(c, e, Border.BackgroundProperty, BackgroundSubscriptionProperty));
        BorderBrushProperty.Changed.AddClassHandler<Control>((c, e) => OnKeyChanged(c, e, Border.BorderBrushProperty, BorderBrushSubscriptionProperty));
    }

    private ThemeBrush()
    {
    }

    public static string? GetForeground(Control control) => control.GetValue(ForegroundProperty);

    public static void SetForeground(Control control, string? key) => control.SetValue(ForegroundProperty, key);

    public static string? GetBackground(Control control) => control.GetValue(BackgroundProperty);

    public static void SetBackground(Control control, string? key) => control.SetValue(BackgroundProperty, key);

    public static string? GetBorderBrush(Control control) => control.GetValue(BorderBrushProperty);

    public static void SetBorderBrush(Control control, string? key) => control.SetValue(BorderBrushProperty, key);

    private static void OnKeyChanged(Control control, AvaloniaPropertyChangedEventArgs e, AvaloniaProperty target, AttachedProperty<IDisposable?> subscriptionProperty)
    {
        control.GetValue(subscriptionProperty)?.Dispose();
        var key = e.GetNewValue<string?>();
        IDisposable? subscription = null;
        if (string.IsNullOrEmpty(key)) control.ClearValue(target);
        else subscription = control.GetResourceObservable(key).Subscribe(new BrushSetter(control, target));
        control.SetValue(subscriptionProperty, subscription);
    }

    private sealed class BrushSetter(Control control, AvaloniaProperty target) : IObserver<object?>
    {
        public void OnNext(object? value)
        {
            if (value is IBrush brush) control.SetValue(target, brush);
            else control.ClearValue(target);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
