using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Visualizers;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class ScriptDebugSession
{
    private const string TopLevelFrameName = "<Top-Level Statements>";
    private const int MaxCallStackFrames = 200;
    private const int MaxInspectDepth = 8;
    private const int MaxInspectNodesPerVariable = 500;
    private const int MaxInspectItems = 50;
    private const int MaxInspectMembers = 30;

    private static ScriptDebugSession? _current;
    public static ScriptDebugSession? Current => _current;

    private static readonly AsyncLocal<DebugFrame?> CurrentFrame = new();

    // User getters and ToString overrides run while locals are captured; their probes must not pause again.
    [ThreadStatic]
    private static bool _isCapturingLocals;

    private readonly object _lock = new();
    private TaskCompletionSource<bool>? _stepGate;
    private CancellationTokenSource? _cts;
    private int _topLevelLine = -1;
    private int _pausedDepth;
    private int _stepOverDepth;

    public string? ScriptId { get; }
    public DebugSessionState State { get; private set; } = DebugSessionState.Idle;
    public DebugStepMode StepMode { get; private set; } = DebugStepMode.None;
    public int PausedLine { get; private set; } = -1;
    public bool IsPaused => State == DebugSessionState.Paused;

    public List<BreakpointItem> Breakpoints { get; } = new();
    public List<DebugVariableItem> CapturedLocals { get; } = new();
    public IReadOnlyList<CallStackFrameItem> CallStack { get; private set; } = Array.Empty<CallStackFrameItem>();

    public event Action<int, IReadOnlyList<DebugVariableItem>>? Paused;
    public event Action? Resumed;
    public event Action? Stopped;

    public static ScriptDebugSession BeginSession(IEnumerable<BreakpointItem> breakpoints, CancellationTokenSource cts, string? scriptId = null)
    {
        EndSession();
        var session = new ScriptDebugSession(breakpoints, cts, scriptId);
        _current = session;
        return session;
    }

    public static void EndSession()
    {
        if (_current != null)
        {
            _current.State = DebugSessionState.Terminated;
            _current._stepGate?.TrySetCanceled();
            _current = null;
        }
    }

    private ScriptDebugSession(IEnumerable<BreakpointItem> breakpoints, CancellationTokenSource cts, string? scriptId = null)
    {
        ScriptId = scriptId;
        Breakpoints.AddRange(breakpoints);
        _cts = cts;
        State = DebugSessionState.Running;
        StepMode = DebugStepMode.None;
    }

    /// <summary>
    /// Static probe method invoked by instrumented C# code at each statement boundary.
    /// </summary>
    public static void Hit(int lineNumber, Func<Dictionary<string, object?>?>? localsFactory)
    {
        _current?.OnStatementHit(lineNumber, localsFactory);
    }

    /// <summary>Called by instrumented code on entry to each body; disposing the scope pops the frame.</summary>
    public static DebugFrameScope EnterFrame(string methodName, int line)
    {
        var parent = CurrentFrame.Value;
        CurrentFrame.Value = new DebugFrame(methodName, line, parent);
        return new DebugFrameScope(parent);
    }

    internal static void PopFrame(DebugFrame? parent) => CurrentFrame.Value = parent;

    internal sealed class DebugFrame
    {
        public DebugFrame(string methodName, int line, DebugFrame? parent)
        {
            MethodName = methodName;
            Line = line;
            Parent = parent;
            Depth = (parent?.Depth ?? 0) + 1;
        }

        public string MethodName { get; }
        public DebugFrame? Parent { get; }
        public int Depth { get; }
        public int Line { get; set; }
    }

    public void OnStatementHit(int lineNumber, Func<Dictionary<string, object?>?>? localsFactory)
    {
        if (_isCapturingLocals)
        {
            return;
        }

        if (State == DebugSessionState.Terminated || _cts?.IsCancellationRequested == true)
        {
            throw new OperationCanceledException("Execution halted by debugger.");
        }

        var frame = CurrentFrame.Value;
        int depth = frame?.Depth ?? 0;
        if (frame != null)
        {
            frame.Line = lineNumber;
        }
        else
        {
            _topLevelLine = lineNumber;
        }

        bool shouldBreak = false;
        var bp = Breakpoints.FirstOrDefault(b => b.IsEnabled && b.LineNumber == lineNumber);

        if (bp != null)
        {
            bp.HitCount++;
            shouldBreak = true;
        }
        else if (StepMode == DebugStepMode.StepInto ||
                 (StepMode == DebugStepMode.StepOver && depth <= _stepOverDepth))
        {
            shouldBreak = true;
        }

        if (!shouldBreak)
        {
            return;
        }

        // We should pause!
        TaskCompletionSource<bool> gate;
        lock (_lock)
        {
            State = DebugSessionState.Paused;
            PausedLine = lineNumber;
            StepMode = DebugStepMode.None;
            _pausedDepth = depth;

            CapturedLocals.Clear();
            if (localsFactory != null)
            {
                _isCapturingLocals = true;
                try
                {
                    var dict = localsFactory.Invoke();
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            CapturedLocals.Add(CreateVariableItem(kvp.Key, kvp.Value));
                        }
                    }
                }
                catch
                {
                    // Ignore transient local variable capture errors
                }
                finally
                {
                    _isCapturingLocals = false;
                }
            }

            CallStack = BuildCallStack(frame);

            _stepGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            gate = _stepGate;
        }

        // Notify UI thread
        var localsSnapshot = CapturedLocals.ToList();
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() =>
            {
                Paused?.Invoke(lineNumber, localsSnapshot);
            });
        }
        else
        {
            Task.Run(() => Paused?.Invoke(lineNumber, localsSnapshot));
        }

        // Wait asynchronously without blocking the UI thread (execution runs on Task.Run worker)
        try
        {
            using var reg = _cts?.Token.Register(() => gate.TrySetCanceled());
            gate.Task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            State = DebugSessionState.Terminated;
            throw;
        }
    }

    public void Continue()
    {
        lock (_lock)
        {
            if (State != DebugSessionState.Paused) return;
            State = DebugSessionState.Running;
            StepMode = DebugStepMode.Continue;
            PausedLine = -1;
            _stepGate?.TrySetResult(true);
        }
        DispatchToUI(Resumed);
    }

    public void StepOver()
    {
        lock (_lock)
        {
            if (State != DebugSessionState.Paused) return;
            State = DebugSessionState.Running;
            StepMode = DebugStepMode.StepOver;
            _stepOverDepth = _pausedDepth;
            PausedLine = -1;
            _stepGate?.TrySetResult(true);
        }
        DispatchToUI(Resumed);
    }

    public void StepInto()
    {
        lock (_lock)
        {
            if (State != DebugSessionState.Paused) return;
            State = DebugSessionState.Running;
            StepMode = DebugStepMode.StepInto;
            PausedLine = -1;
            _stepGate?.TrySetResult(true);
        }
        DispatchToUI(Resumed);
    }

    public void Stop()
    {
        lock (_lock)
        {
            State = DebugSessionState.Terminated;
            PausedLine = -1;
            _cts?.Cancel();
            _stepGate?.TrySetCanceled();
        }
        DispatchToUI(Stopped);
    }

    private static void DispatchToUI(Action? action)
    {
        if (action == null) return;
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.Post(action);
        else
            action();
    }

    private IReadOnlyList<CallStackFrameItem> BuildCallStack(DebugFrame? current)
    {
        var frames = new List<CallStackFrameItem>();
        for (var frame = current; frame != null; frame = frame.Parent)
        {
            if (frames.Count == MaxCallStackFrames)
            {
                int hidden = frame.Depth + (_topLevelLine > 0 ? 1 : 0);
                frames.Add(CreateFrameItem(frames.Count, $"… {hidden} more frames", 0));
                return frames;
            }

            frames.Add(CreateFrameItem(frames.Count, frame.MethodName, frame.Line));
        }

        if (_topLevelLine > 0)
        {
            frames.Add(CreateFrameItem(frames.Count, TopLevelFrameName, _topLevelLine));
        }

        return frames;
    }

    private static CallStackFrameItem CreateFrameItem(int index, string methodName, int line) => new()
    {
        FrameIndex = index,
        MethodName = methodName,
        LineNumber = line,
        IsCurrentFrame = index == 0
    };

    private static DebugVariableItem CreateVariableItem(string name, object? value)
    {
        int budget = MaxInspectNodesPerVariable;
        return CreateVariableItem(name, value, 0, name, new HashSet<object>(ReferenceEqualityComparer.Instance), ref budget);
    }

    private static DebugVariableItem CreateVariableItem(
        string name,
        object? value,
        int depth,
        string path,
        HashSet<object> ancestors,
        ref int budget)
    {
        budget--;
        var item = new DebugVariableItem
        {
            Name = name,
            RawValue = value,
            PathExpression = path,
            NodeKind = depth == 0 ? "Local" : (name.StartsWith('[') ? "CollectionItem" : "Property")
        };

        if (value == null)
        {
            item.TypeName = "null";
            item.ValueDisplay = "null";
            return item;
        }

        var type = value.GetType();
        item.TypeName = GetFriendlyTypeName(type);
        item.ValueDisplay = FormatValueDisplay(value);

        if (value is IEnumerable && !(value is string))
        {
            item.IsCollection = true;
            if (value is Array arr)
            {
                item.CollectionItemCount = arr.Length;
            }
            else if (value is ICollection coll)
            {
                item.CollectionItemCount = coll.Count;
            }
        }
        else if (value is string str)
        {
            item.IsTextOrStructured = str.Length > 20 || str.Contains('\n') || str.TrimStart().StartsWith('{') || str.TrimStart().StartsWith('<');
        }

        if (ObjectInspectorBuilder.IsScalarType(type) || depth >= MaxInspectDepth || budget <= 0)
        {
            return item;
        }

        // Linked nodes point back at their ancestors (Prev, Parent, graph neighbours); stop at the first repeat.
        if (!ancestors.Add(value))
        {
            item.ValueDisplay += " ↻ (circular reference)";
            return item;
        }

        try
        {
            foreach (var (childName, childValue) in GetChildren(value, type))
            {
                if (budget <= 0) break;
                string childPath = childName.StartsWith('[') ? $"{path}{childName}" : $"{path}.{childName}";
                item.Children.Add(CreateVariableItem(childName, childValue, depth + 1, childPath, ancestors, ref budget));
            }
        }
        finally
        {
            ancestors.Remove(value);
        }

        item.HasChildren = item.Children.Count > 0;
        item.ChildrenLoaded = true;

        return item;
    }

    /// <summary>
    /// Lazily expands a variable's children on demand using the registered type visualizer pipeline.
    /// </summary>
    public static IReadOnlyList<DebugVariableItem> ExpandVariableChildren(DebugVariableItem parent)
    {
        if (parent == null || parent.RawValue == null)
            return Array.Empty<DebugVariableItem>();

        if (parent.ChildrenLoaded && parent.Children.Count > 0)
            return parent.Children;

        var val = parent.RawValue;
        var type = val.GetType();
        if (ObjectInspectorBuilder.IsScalarType(type))
        {
            parent.HasChildren = false;
            parent.ChildrenLoaded = true;
            return Array.Empty<DebugVariableItem>();
        }

        int budget = MaxInspectNodesPerVariable;
        var ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var children = new List<DebugVariableItem>();

        foreach (var (childName, childValue) in GetChildren(val, type))
        {
            string childPath = childName.StartsWith('[') ? $"{parent.PathExpression}{childName}" : $"{parent.PathExpression}.{childName}";
            children.Add(CreateVariableItem(childName, childValue, 1, childPath, ancestors, ref budget));
        }

        parent.Children.Clear();
        foreach (var child in children)
        {
            parent.Children.Add(child);
        }
        parent.HasChildren = parent.Children.Count > 0;
        parent.ChildrenLoaded = true;

        return parent.Children;
    }

    private static List<(string Name, object? Value)> GetChildren(object value, Type type)
    {
        var visualizer = DebugTypeVisualizerRegistry.Default.FindVisualizer(value, type);
        if (visualizer != null)
        {
            try
            {
                return visualizer.GetChildren(value, type).ToList();
            }
            catch
            {
                return new List<(string Name, object? Value)>();
            }
        }
        return new List<(string Name, object? Value)>();
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genName = type.Name.Split('`')[0];
            var args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{genName}<{args}>";
        }
        return type.Name;
    }

    private static string FormatValueDisplay(object val)
    {
        try
        {
            if (val is string s) return $"\"{s}\"";
            if (val is bool b) return b ? "true" : "false";
            if (val is char c) return $"'{c}'";

            var type = val.GetType();
            var visualizer = DebugTypeVisualizerRegistry.Default.FindVisualizer(val, type);
            if (visualizer != null)
            {
                return visualizer.FormatSummary(val, type);
            }
            return val.ToString() ?? type.Name;
        }
        catch (Exception ex)
        {
            // e.g. a record's generated ToString recursing through a cycle
            return $"{{{GetFriendlyTypeName(val.GetType())}}} (ToString threw {ex.GetType().Name})";
        }
    }
}
