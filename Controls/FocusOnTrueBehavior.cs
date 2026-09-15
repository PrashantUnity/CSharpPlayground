using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

/// <summary>
/// Lightweight attached-property alternative to pulling in Avalonia.Xaml.Interactivity for one
/// behavior: focuses (and, for a TextBox, selects all text in) the element whenever the bound flag
/// flips to true — e.g. giving the inline explorer rename box keyboard focus as soon as IsRenaming
/// becomes true, instead of requiring an extra manual click before the user can type.
/// </summary>
public static class FocusOnTrueBehavior
{
    public static readonly AttachedProperty<bool> IsActiveProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("IsActive", typeof(FocusOnTrueBehavior));

    static FocusOnTrueBehavior()
    {
        IsActiveProperty.Changed.AddClassHandler<InputElement>((element, e) =>
        {
            if (e.NewValue is not true) return;

            Dispatcher.UIThread.Post(() =>
            {
                element.Focus();
                if (element is TextBox textBox)
                {
                    textBox.SelectAll();
                }
            }, DispatcherPriority.Loaded);
        });
    }

    public static bool GetIsActive(InputElement element) => element.GetValue(IsActiveProperty);
    public static void SetIsActive(InputElement element, bool value) => element.SetValue(IsActiveProperty, value);
}
