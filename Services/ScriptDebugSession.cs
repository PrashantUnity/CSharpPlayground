using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public enum DebugStepMode
{
    None,
    StepOver,
    StepInto,
    Continue
}

public enum DebugSessionState
{
    Idle,
    Running,
    Paused,
    Terminated
}

public class ScriptDebugSession
{
    private static ScriptDebugSession? _current;
    public static ScriptDebugSession? Current => _current;

    private readonly object _lock = new();
    private TaskCompletionSource<bool>? _stepGate;
    private CancellationTokenSource? _cts;

    public string? ScriptId { get; }
    public DebugSessionState State { get; private set; } = DebugSessionState.Idle;
    public DebugStepMode StepMode { get; private set; } = DebugStepMode.None;
    public int PausedLine { get; private set; } = -1;
    public bool IsPaused => State == DebugSessionState.Paused;

    public List<BreakpointItem> Breakpoints { get; } = new();
    public List<DebugVariableItem> CapturedLocals { get; } = new();
    public List<CallStackFrameItem> CallStack { get; } = new();

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

    public void OnStatementHit(int lineNumber, Func<Dictionary<string, object?>?>? localsFactory)
    {
        if (State == DebugSessionState.Terminated || _cts?.IsCancellationRequested == true)
        {
            throw new OperationCanceledException("Execution halted by debugger.");
        }

        bool shouldBreak = false;
        var bp = Breakpoints.FirstOrDefault(b => b.IsEnabled && b.LineNumber == lineNumber);

        if (bp != null)
        {
            bp.HitCount++;
            shouldBreak = true;
        }
        else if (StepMode == DebugStepMode.StepOver || StepMode == DebugStepMode.StepInto)
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

            CapturedLocals.Clear();
            if (localsFactory != null)
            {
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
            }

            CallStack.Clear();
            CallStack.Add(new CallStackFrameItem
            {
                FrameIndex = 0,
                MethodName = "<Script Main>",
                LineNumber = lineNumber,
                FileName = "script.cs",
                IsCurrentFrame = true
            });

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
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Resumed?.Invoke());
        }
        else
        {
            Resumed?.Invoke();
        }
    }

    public void StepOver()
    {
        lock (_lock)
        {
            if (State != DebugSessionState.Paused) return;
            State = DebugSessionState.Running;
            StepMode = DebugStepMode.StepOver;
            PausedLine = -1;
            _stepGate?.TrySetResult(true);
        }
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Resumed?.Invoke());
        }
        else
        {
            Resumed?.Invoke();
        }
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
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Resumed?.Invoke());
        }
        else
        {
            Resumed?.Invoke();
        }
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
        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Stopped?.Invoke());
        }
        else
        {
            Stopped?.Invoke();
        }
    }

    private static DebugVariableItem CreateVariableItem(string name, object? value)
    {
        var item = new DebugVariableItem
        {
            Name = name,
            RawValue = value
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

        // Add children for collections or structured objects
        try
        {
            if (value is IDictionary dict)
            {
                int count = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    if (count++ >= 50) break;
                    item.Children.Add(CreateVariableItem($"[{entry.Key}]", entry.Value));
                }
            }
            else if (value is IEnumerable enumerable and not string)
            {
                int idx = 0;
                foreach (var element in enumerable)
                {
                    if (idx >= 50) break;
                    item.Children.Add(CreateVariableItem($"[{idx++}]", element));
                }
            }
            else if (!type.IsPrimitive && type != typeof(string) && type != typeof(decimal) && type != typeof(DateTime))
            {
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                                .Take(30);

                foreach (var prop in props)
                {
                    try
                    {
                        var propVal = prop.GetValue(value);
                        item.Children.Add(CreateVariableItem(prop.Name, propVal));
                    }
                    catch
                    {
                        // Ignore property evaluation errors
                    }
                }
            }
        }
        catch
        {
            // Ignore child inspection errors
        }

        return item;
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
        if (val is string s) return $"\"{s}\"";
        if (val is bool b) return b ? "true" : "false";
        if (val is char c) return $"'{c}'";
        if (val is ICollection col) return $"Count = {col.Count}";
        return val.ToString() ?? val.GetType().Name;
    }
}
