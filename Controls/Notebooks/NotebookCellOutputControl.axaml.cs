using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Notebooks;

public partial class NotebookCellOutputControl : UserControl
{
    public NotebookCellOutputControl()
    {
        InitializeComponent();
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true, RoutingStrategies.Bubble, handledEventsToo: true);
    }
}
