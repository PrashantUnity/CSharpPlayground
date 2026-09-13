using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var settingsStore = new StandaloneSettingsStore();
        var sp = new StandaloneServiceProvider(settingsStore);

        var vm = new CSharpStudioHostViewModel(sp);
        vm.RequestClose = () => Close();
        StudioHost.DataContext = vm;
    }
}
