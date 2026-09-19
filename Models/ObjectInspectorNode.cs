using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// Represents a row in an object inspector, displaying a property or field name and its value.
/// If the value is a complex object, ChildNode contains the nested expandable ObjectInspectorNode.
/// </summary>
public partial class ObjectInspectorPropertyRow : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _simpleValueText = string.Empty;

    [ObservableProperty]
    private bool _isNull;

    [ObservableProperty]
    private bool _isComplexChild;

    [ObservableProperty]
    private ObjectInspectorNode? _childNode;

    public string ValueForeground
    {
        get
        {
            if (IsNull) return "#808080";
            if (string.IsNullOrEmpty(SimpleValueText)) return "#E2E2E6";
            if (SimpleValueText.StartsWith("\"") || SimpleValueText.StartsWith("'")) return "#CE9178";
            if (bool.TryParse(SimpleValueText, out _)) return "#569CD6";
            if (char.IsDigit(SimpleValueText[0]) || (SimpleValueText.Length > 1 && (SimpleValueText[0] == '-' || SimpleValueText[0] == '+') && char.IsDigit(SimpleValueText[1])))
                return "#B5CEA8";
            if (SimpleValueText.StartsWith("[") && SimpleValueText.EndsWith("]")) return "#4EC9B0";
            return "#D4D4D4";
        }
    }

    partial void OnSimpleValueTextChanged(string value)
    {
        OnPropertyChanged(nameof(ValueForeground));
    }

    partial void OnIsNullChanged(bool value)
    {
        OnPropertyChanged(nameof(ValueForeground));
    }
}

/// <summary>
/// Represents a collapsible interactive object inspector node in a notebook cell output,
/// displaying a header banner (e.g. ▼ Submission#16+People) and its key-value property rows.
/// </summary>
public partial class ObjectInspectorNode : ObservableObject
{
    [ObservableProperty]
    private string _headerTitle = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isExpandable = true;

    [ObservableProperty]
    private bool _isCircularReference;

    public ObservableCollection<ObjectInspectorPropertyRow> Properties { get; } = new();

    public string ToggleIcon => IsExpanded ? "▼" : "▶";

    public ObjectInspectorNode()
    {
    }

    public ObjectInspectorNode(string headerTitle, bool isExpanded = true, bool isExpandable = true)
    {
        _headerTitle = headerTitle;
        _isExpanded = isExpanded;
        _isExpandable = isExpandable;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ToggleIcon));
    }

    [RelayCommand]
    public void ToggleExpand()
    {
        if (IsExpandable)
        {
            IsExpanded = !IsExpanded;
        }
    }
}
