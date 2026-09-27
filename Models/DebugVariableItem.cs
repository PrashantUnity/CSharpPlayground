using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public partial class DebugVariableItem : ObservableObject
{
    public DebugVariableItem()
    {
        Children.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CanExpand));
            if (Children.Count > 0)
            {
                HasChildren = true;
                ChildrenLoaded = true;
            }
        };
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconKind))]
    private string _typeName = "object";

    [ObservableProperty]
    private string _valueDisplay = "null";

    [ObservableProperty]
    private object? _rawValue;

    [ObservableProperty]
    private string _kind = "Local"; // Local, Parameter, Return, Watch

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExpand))]
    [NotifyPropertyChangedFor(nameof(IconKind))]
    private bool _hasChildren;

    [ObservableProperty]
    private bool _isLoadingChildren;

    [ObservableProperty]
    private bool _childrenLoaded;

    [ObservableProperty]
    private bool _hasValueChanged;

    [ObservableProperty]
    private string? _previousValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconKind))]
    private bool _isCollection;

    [ObservableProperty]
    private int? _collectionItemCount;

    private bool _isTextOrStructured;
    public bool IsTextOrStructured
    {
        get => _isTextOrStructured || (!string.IsNullOrWhiteSpace(ValueDisplay) &&
               (ValueDisplay.Contains('\n') || ValueDisplay.TrimStart().StartsWith('{') || ValueDisplay.TrimStart().StartsWith('<')));
        set => SetProperty(ref _isTextOrStructured, value);
    }

    [ObservableProperty]
    private string? _pathExpression;

    [ObservableProperty]
    private int _variablesReference;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconKind))]
    private string _nodeKind = "Local"; // Local, Parameter, Property, Field, CollectionItem, StaticMember, RawView, Watch

    public ObservableCollection<DebugVariableItem> Children { get; } = new();

    public bool CanExpand => HasChildren || Children.Count > 0;

    public string IconKind => NodeKind switch
    {
        "RawView" => "CodeBraces",
        "StaticMember" => "LightningBoltOutline",
        "CollectionItem" => "FormatListNumbered",
        _ when TypeName.Contains("Http", StringComparison.OrdinalIgnoreCase) => "Web",
        _ when IsCollection => "Table",
        _ when TypeName.Contains("string", StringComparison.OrdinalIgnoreCase) || TypeName.Contains("str", StringComparison.OrdinalIgnoreCase) => "FormatQuoteClose",
        _ when TypeName.Contains("int", StringComparison.OrdinalIgnoreCase) || TypeName.Contains("long", StringComparison.OrdinalIgnoreCase) || TypeName.Contains("float", StringComparison.OrdinalIgnoreCase) || TypeName.Contains("double", StringComparison.OrdinalIgnoreCase) || TypeName.Contains("decimal", StringComparison.OrdinalIgnoreCase) || TypeName.Contains("num", StringComparison.OrdinalIgnoreCase) => "Numeric",
        _ when TypeName.Contains("bool", StringComparison.OrdinalIgnoreCase) => "ToggleSwitchOutline",
        _ when HasChildren || Children.Count > 0 => "CubeOutline",
        _ => "Variable"
    };
}

