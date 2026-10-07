using Avalonia.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Activities;

/// <summary>The stack of activity toasts in the studio's bottom-right corner (bound to the host's activity presenter).</summary>
public partial class ActivityToastHost : UserControl
{
    public ActivityToastHost()
    {
        InitializeComponent();
    }
}
