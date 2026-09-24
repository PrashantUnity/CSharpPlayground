using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>Draws recursion as a call tree; the path from main to the current call is the call stack.</summary>
public sealed class RecursionTracker
{
    public const int MaxCalls = 255;
    private const int MaxLabelLength = 12;

    private readonly TreeNodeData _root = new("main", "call_0");
    private readonly Stack<TreeNodeData> _stack = new();
    private readonly WatchList _watches = new();
    private int _memoHits;
    private bool _limitReached;

    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    /// <summary>Calls drawn so far (at most <see cref="MaxCalls"/>): a quick way to compare naive and memoized versions.</summary>
    public int CallCount { get; private set; }

    private RecursionTracker(string title, int sourceLine, string sourceFile)
    {
        Sequence = new VisualizerSequence();
        Options = new VisualizerOptions
        {
            Title = title,
            Kind = VisualizerKind.Tree,
            TreeData = _root,
            Sequence = Sequence
        };
        Record("Program starts in main", sourceLine, sourceFile);
    }

    public static RecursionTracker Create(
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "") =>
        new(title ?? "Recursion Tree", sourceLine, sourceFile);

    /// <summary>Shows a live memo, set or list beneath the tree at every step recorded after this call.</summary>
    public void Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "") =>
        _watches.Add(collection, name);

    /// <summary>Starts a call; use it as <c>using var call = calls.Enter($"fib({n})");</c> so it always ends.</summary>
    public RecursionCall Enter(
        object? label,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (CallCount >= MaxCalls)
        {
            if (!_limitReached)
            {
                Record($"Stopped drawing after {MaxCalls} calls; the recursion keeps running", sourceLine, sourceFile);
                _limitReached = true;
            }
            return new RecursionCall(this, null);
        }

        var caller = _stack.Count > 0 ? _stack.Peek() : _root;
        caller.State = TreeNodeState.Candidate;
        caller.IsActive = false;

        var node = new TreeNodeData(Shorten(label?.ToString() ?? "null"), $"call_{++CallCount}")
        {
            Parent = caller,
            Depth = caller.Depth + 1,
            State = TreeNodeState.Current,
            IsActive = true
        };
        caller.Children.Add(node);
        _stack.Push(node);

        Record($"Call {node.DisplayValue}", sourceLine, sourceFile);
        return new RecursionCall(this, node);
    }

    internal void Complete(TreeNodeData node, TreeNodeState finalState, string? subLabel, string description, int sourceLine, string sourceFile)
    {
        if (!_stack.Contains(node)) return;

        // Frames above this one were unwound without finishing (an exception, or an outer call returned first).
        while (_stack.Count > 0 && !ReferenceEquals(_stack.Peek(), node))
        {
            var abandoned = _stack.Pop();
            abandoned.State = TreeNodeState.Visited;
            abandoned.IsActive = false;
        }

        _stack.Pop();
        node.State = finalState;
        node.IsActive = false;
        node.SubLabel = subLabel;
        if (finalState == TreeNodeState.Matched) _memoHits++;

        var caller = _stack.Count > 0 ? _stack.Peek() : null;
        if (caller != null)
        {
            caller.State = TreeNodeState.Current;
            caller.IsActive = true;
        }
        else
        {
            _root.State = TreeNodeState.Default;
        }

        Record(description, sourceLine, sourceFile);
    }

    internal static string Describe(object? value)
    {
        var text = value == null ? "null"
            : ObjectInspectorBuilder.IsScalarType(value.GetType()) ? ObjectInspectorBuilder.FormatScalarValue(value)
            : value.ToString() ?? value.GetType().Name;
        return Shorten(text);
    }

    private static string Shorten(string text) =>
        text.Length <= MaxLabelLength ? text : text[..(MaxLabelLength - 1)] + "…";

    private void Record(string description, int sourceLine, string sourceFile)
    {
        if (_limitReached) return;

        var step = new VisualizerStep(Sequence.TotalSteps, description, VisualizerKind.Tree)
        {
            Snapshot = _root.Clone(),
            SourceLine = sourceLine,
            SourceFile = sourceFile,
            Watches = _watches.Capture()
        };

        if (_stack.Count > 0) step.ActiveNodeIds.Add(_stack.Peek().Id);
        step.AuxiliaryInfo["Depth"] = _stack.Count.ToString();
        step.AuxiliaryInfo["Calls"] = CallCount.ToString();
        if (_memoHits > 0) step.AuxiliaryInfo["Memo hits"] = _memoHits.ToString();
        Sequence.AddStep(step);
    }
}

/// <summary>One call in a <see cref="RecursionTracker"/>; finish it with Return, Memo or Prune, or let the using end it.</summary>
public sealed class RecursionCall : IDisposable
{
    private readonly RecursionTracker _tracker;
    private readonly TreeNodeData? _node;
    private bool _completed;

    internal RecursionCall(RecursionTracker tracker, TreeNodeData? node)
    {
        _tracker = tracker;
        _node = node;
    }

    public T Return<T>(T value, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
    {
        if (TryComplete())
        {
            string text = RecursionTracker.Describe(value);
            _tracker.Complete(_node!, TreeNodeState.Visited, $"= {text}", $"{_node!.DisplayValue} returns {text}", sourceLine, sourceFile);
        }
        return value;
    }

    /// <summary>Ends the call with a value that was already computed (a memo or cache hit).</summary>
    public T Memo<T>(T value, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
    {
        if (TryComplete())
        {
            string text = RecursionTracker.Describe(value);
            _tracker.Complete(_node!, TreeNodeState.Matched, $"memo {text}", $"{_node!.DisplayValue} is already in the memo: {text}", sourceLine, sourceFile);
        }
        return value;
    }

    /// <summary>Ends a backtracking branch that cannot lead to a solution.</summary>
    public void Prune(string? reason = null, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
    {
        if (TryComplete())
        {
            string description = reason == null ? $"Prune {_node!.DisplayValue}" : $"Prune {_node!.DisplayValue}: {reason}";
            _tracker.Complete(_node, TreeNodeState.Pruned, "pruned", description, sourceLine, sourceFile);
        }
    }

    public void Dispose()
    {
        if (TryComplete())
        {
            _tracker.Complete(_node!, TreeNodeState.Visited, null, $"{_node!.DisplayValue} returns", 0, string.Empty);
        }
    }

    private bool TryComplete()
    {
        if (_node == null || _completed) return false;
        _completed = true;
        return true;
    }
}
