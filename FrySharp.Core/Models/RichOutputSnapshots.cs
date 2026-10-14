using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

// Plain, JSON-safe snapshots of the two rich cell outputs that are otherwise pure ViewModel/UI types
// (DumpTableResult/ObjectInspectorNode are ObservableObject with CommunityToolkit-generated
// ICommand properties, and DumpTableCell.RawValue can hold an arbitrary, possibly non-serializable
// user object) — mirrors the existing pattern for Image/Html output: persist plain data, rehydrate a
// live object on load. DumpTableCell.RawValue is intentionally dropped (used only by the live "Copy as
// JSON" export); everything needed to redraw the table/inspector identically is kept.

public class DumpTableSnapshot
{
    public string Title { get; set; } = string.Empty;
    public string? Label { get; set; }
    public List<DumpTableSnapshotColumn> Columns { get; set; } = new();
    public List<DumpTableSnapshotRow> Rows { get; set; } = new();
}

public class DumpTableSnapshotColumn
{
    public string Header { get; set; } = string.Empty;
    public bool IsNumeric { get; set; }
}

public class DumpTableSnapshotRow
{
    public List<DumpTableSnapshotCell> Cells { get; set; } = new();
}

public class DumpTableSnapshotCell
{
    public string DisplayText { get; set; } = string.Empty;
    public bool IsNumeric { get; set; }
    public bool IsBoolean { get; set; }
    public bool IsNull { get; set; }
}

public class ObjectInspectorSnapshot
{
    public string HeaderTitle { get; set; } = string.Empty;
    public bool IsExpandable { get; set; } = true;
    public bool IsCircularReference { get; set; }
    public List<ObjectInspectorSnapshotProperty> Properties { get; set; } = new();
}

public class ObjectInspectorSnapshotProperty
{
    public string Name { get; set; } = string.Empty;
    public string SimpleValueText { get; set; } = string.Empty;
    public bool IsNull { get; set; }
    public bool IsComplexChild { get; set; }
    public ObjectInspectorSnapshot? ChildNode { get; set; }
}

public static class DumpTableSnapshotConverter
{
    public static DumpTableSnapshot ToSnapshot(this DumpTableResult table)
    {
        var snapshot = new DumpTableSnapshot
        {
            Title = table.Title,
            Label = table.Label
        };

        foreach (var col in table.Columns)
        {
            snapshot.Columns.Add(new DumpTableSnapshotColumn { Header = col.Header, IsNumeric = col.IsNumeric });
        }

        foreach (var row in table.Rows)
        {
            snapshot.Rows.Add(new DumpTableSnapshotRow
            {
                Cells = row.Cells.Select(c => new DumpTableSnapshotCell
                {
                    DisplayText = c.DisplayText,
                    IsNumeric = c.IsNumeric,
                    IsBoolean = c.IsBoolean,
                    IsNull = c.IsNull
                }).ToList()
            });
        }

        return snapshot;
    }

    public static DumpTableResult ToLive(this DumpTableSnapshot snapshot)
    {
        var table = new DumpTableResult(snapshot.Title, snapshot.Label);

        foreach (var col in snapshot.Columns)
        {
            table.Columns.Add(new DumpTableColumn { Header = col.Header, IsNumeric = col.IsNumeric });
        }

        var rowIndex = 0;
        foreach (var row in snapshot.Rows)
        {
            var cells = row.Cells.Select(c => new DumpTableCell
            {
                DisplayText = c.DisplayText,
                IsNumeric = c.IsNumeric,
                IsBoolean = c.IsBoolean,
                IsNull = c.IsNull
            }).ToList();
            table.Rows.Add(new DumpTableRow(rowIndex++, cells));
        }

        return table;
    }
}

public static class ObjectInspectorSnapshotConverter
{
    public static ObjectInspectorSnapshot ToSnapshot(this ObjectInspectorNode node)
    {
        var snapshot = new ObjectInspectorSnapshot
        {
            HeaderTitle = node.HeaderTitle,
            IsExpandable = node.IsExpandable,
            IsCircularReference = node.IsCircularReference
        };

        foreach (var prop in node.Properties)
        {
            snapshot.Properties.Add(new ObjectInspectorSnapshotProperty
            {
                Name = prop.Name,
                SimpleValueText = prop.SimpleValueText,
                IsNull = prop.IsNull,
                IsComplexChild = prop.IsComplexChild,
                ChildNode = prop.ChildNode?.ToSnapshot()
            });
        }

        return snapshot;
    }

    public static ObjectInspectorNode ToLive(this ObjectInspectorSnapshot snapshot)
    {
        var node = new ObjectInspectorNode(snapshot.HeaderTitle, isExpanded: true, isExpandable: snapshot.IsExpandable)
        {
            IsCircularReference = snapshot.IsCircularReference
        };

        foreach (var prop in snapshot.Properties)
        {
            node.Properties.Add(new ObjectInspectorPropertyRow
            {
                Name = prop.Name,
                SimpleValueText = prop.SimpleValueText,
                IsNull = prop.IsNull,
                IsComplexChild = prop.IsComplexChild,
                ChildNode = prop.ChildNode?.ToLive()
            });
        }

        return node;
    }
}
