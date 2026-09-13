using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public partial class DebugVariableItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _typeName = "object";

    [ObservableProperty]
    private string _valueDisplay = "null";

    [ObservableProperty]
    private object? _rawValue;

    [ObservableProperty]
    private string _kind = "Local"; // Local, Parameter, Return, Watch

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<DebugVariableItem> Children { get; } = new();

    public bool CanExpand => Children.Count > 0;
}
