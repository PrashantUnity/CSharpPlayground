using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

// Tree: the recorder's factory and step for it, and its typed recorder.
public partial class VisualizerRecorder
{
    public static TreeVisualizerRecorder CreateTree(
        object root,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var treeData = TreeDataParser.Parse(root);
        var options = new VisualizerOptions
        {
            Title = title ?? "Tree Visualizer",
            Kind = VisualizerKind.Tree,
            TreeData = treeData
        };
        var recorder = new TreeVisualizerRecorder(options);
        recorder.Step("Initial Tree State", sourceLine, sourceFile);
        return recorder;
    }

    public VisualizerRecorder StepTree(
        string description,
        Action<TreeNodeData>? updateTree = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (Options.TreeData != null)
        {
            updateTree?.Invoke(Options.TreeData);

            var step = NewStep(description, sourceLine, sourceFile);
            step.Snapshot = Options.TreeData.Clone();
            Sequence.AddStep(step);
        }
        return this;
    }
}

public class TreeVisualizerRecorder : VisualizerRecorder
{
    public TreeVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public TreeVisualizerRecorder Step(
        string description,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepTree(description, null, sourceLine, sourceFile);
        return this;
    }

    public TreeVisualizerRecorder Step(
        string description,
        Action<TreeNodeData> updateTree,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepTree(description, updateTree, sourceLine, sourceFile);
        return this;
    }
}
