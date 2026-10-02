using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class AssemblyReferenceViewModel : ObservableObject
{
    public AssemblyReferenceItem Model { get; }

    public string Name => Model.Name;
    public string AssemblyPath => Model.AssemblyPath;
    public string Description => Model.Description;
    public bool IsSystem => Model.IsSystem;

    [ObservableProperty]
    private bool _isEnabled;

    public AssemblyReferenceViewModel(AssemblyReferenceItem model)
    {
        Model = model;
        _isEnabled = model.IsEnabled;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        Model.IsEnabled = value;
    }
}
