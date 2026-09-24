using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class ScriptDebuggerServiceTests
{
    private readonly RoslynCompilerService _compiler = new();
    private readonly ScriptExecutionEngine _engine = new();

    private ScriptDebuggerService CreateDebugger() => new(_compiler, _engine);

    [Fact]
    public void CompileForDebugging_TwoSumAlgorithmTemplate_SucceedsWithZeroErrorsAndGeneratesAssembly()
    {
        var debugger = CreateDebugger();
        var code = @"using System;
using System.Collections.Generic;

public class Solution 
{
    public int[] TwoSum(int[] nums, int target) 
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < nums.Length; i++) 
        {
            int complement = target - nums[i];
            if (map.TryGetValue(complement, out int index)) 
            {
                return new int[] { index, i };
            }
            map[nums[i]] = i;
        }
        return Array.Empty<int>();
    }
}

// Execute test cases
var sol = new Solution();

int[] test1 = sol.TwoSum(new int[] { 2, 7, 11, 15 }, 9);
test1.Dump(""Test Case 1 (Target = 9)"");

int[] test2 = sol.TwoSum(new int[] { 3, 2, 4 }, 6);
test2.Dump(""Test Case 2 (Target = 6)"");

Console.WriteLine(""All test cases evaluated successfully."");";

        var (success, bytes, diagnostics) = debugger.CompileForDebugging(code, ExecutionLanguageMode.Statements);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(success, $"Compilation failed with {errors.Count} error(s): {string.Join("; ", errors.Select(e => e.Message))}");
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task DebugExecution_TwoSumAlgorithm_HitsBreakpointInsideClassMethodWithParametersAndLocals()
    {
        var debugger = CreateDebugger();
        var code = @"using System;
using System.Collections.Generic;

public class Solution 
{
    public int[] TwoSum(int[] nums, int target) 
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < nums.Length; i++) 
        {
            int complement = target - nums[i];
            if (map.TryGetValue(complement, out int index)) 
            {
                return new int[] { index, i };
            }
            map[nums[i]] = i;
        }
        return Array.Empty<int>();
    }
}

var sol = new Solution();
int[] test1 = sol.TwoSum(new int[] { 2, 7, 11, 15 }, 9);
Console.WriteLine(""Finished"");";

        var (success, bytes, diagnostics) = debugger.CompileForDebugging(code, ExecutionLanguageMode.Statements);
        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));
        Assert.NotNull(bytes);

        // Set breakpoint on line 11: `int complement = target - nums[i];`
        var breakpoints = new List<BreakpointItem>
        {
            new() { LineNumber = 11, IsEnabled = true }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var session = ScriptDebugSession.BeginSession(breakpoints, cts, "two-sum-test");

        int hitCount = 0;
        int hitLine = -1;
        IReadOnlyList<DebugVariableItem>? capturedLocals = null;

        session.Paused += (line, locals) =>
        {
            hitCount++;
            hitLine = line;
            capturedLocals = locals;
            // Continue execution to let it complete
            session.Continue();
        };

        try
        {
            var result = await _engine.ExecuteAsync(bytes!, _ => { }, cts.Token);
            Assert.True(result.Success, result.Error);
            Assert.True(hitCount >= 1, "Breakpoint was not hit inside Solution.TwoSum");
            Assert.Equal(11, hitLine);
            Assert.NotNull(capturedLocals);

            // Verify method parameters and local variables are present in debug locals
            Assert.Contains(capturedLocals, l => l.Name == "nums");
            Assert.Contains(capturedLocals, l => l.Name == "target");
            Assert.Contains(capturedLocals, l => l.Name == "map");
            Assert.Contains(capturedLocals, l => l.Name == "i");
        }
        finally
        {
            ScriptDebugSession.EndSession();
        }
    }

    [Fact]
    public void CompileForDebugging_RefParametersAndSpan_ExcludesUnsafeTypesFromProbesWithoutCompilerErrors()
    {
        var debugger = CreateDebugger();
        var code = @"using System;

public class SpanHelper
{
    public static int Process(ref int count, in int limit, out int remainder)
    {
        remainder = count % limit;
        Span<int> buffer = stackalloc int[4];
        buffer[0] = 10;
        buffer[1] = 20;
        int total = buffer[0] + buffer[1];
        count += total;
        return count;
    }
}

int count = 5;
int remainder;
int limit = 3;
int result = SpanHelper.Process(ref count, in limit, out remainder);
Console.WriteLine($""result={result}, remainder={remainder}"");";

        var (success, bytes, diagnostics) = debugger.CompileForDebugging(code, ExecutionLanguageMode.Statements);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(success, $"Compilation with ref/span failed: {string.Join("; ", errors.Select(e => e.Message))}");
        Assert.NotNull(bytes);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task DebugExecution_TopLevelStatements_PausesAndCapturesTopLevelVariables()
    {
        var debugger = CreateDebugger();
        var code = @"int a = 10;
int b = 25;
int c = a + b;
Console.WriteLine(c);";

        var (success, bytes, diagnostics) = debugger.CompileForDebugging(code, ExecutionLanguageMode.Statements);
        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));
        Assert.NotNull(bytes);

        var breakpoints = new List<BreakpointItem>
        {
            new() { LineNumber = 3, IsEnabled = true }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var session = ScriptDebugSession.BeginSession(breakpoints, cts, "top-level-test");

        bool hit = false;
        IReadOnlyList<DebugVariableItem>? captured = null;

        session.Paused += (line, locals) =>
        {
            hit = true;
            captured = locals;
            session.Continue();
        };

        try
        {
            var result = await _engine.ExecuteAsync(bytes!, _ => { }, cts.Token);
            Assert.True(result.Success, result.Error);
            Assert.True(hit);
            Assert.NotNull(captured);
            var varA = captured.FirstOrDefault(v => v.Name == "a");
            var varB = captured.FirstOrDefault(v => v.Name == "b");
            Assert.NotNull(varA);
            Assert.NotNull(varB);
            Assert.Equal("10", varA.ValueDisplay);
            Assert.Equal("25", varB.ValueDisplay);
        }
        finally
        {
            ScriptDebugSession.EndSession();
        }
    }

    [Fact]
    public async Task DebugExecution_DoublyLinkedAndFieldNodes_CapturesLocalsWithoutStackOverflow()
    {
        var code = @"var a = new DNode { Val = 1 };
var b = new DNode { Val = 2, Prev = a };
a.Next = b;
var head = new FieldNode { val = 7, next = new FieldNode { val = 8 } };
Console.WriteLine(a.Val + b.Val + head.val);

public class DNode
{
    public int Val { get; set; }
    public DNode? Next { get; set; }
    public DNode? Prev { get; set; }
}

public class FieldNode
{
    public int val;
    public FieldNode? next;
}";

        IReadOnlyList<DebugVariableItem>? captured = null;
        var lines = await RunWithBreakpointsAsync(code, new[] { 5 }, (session, _) =>
        {
            captured = session.CapturedLocals.ToList();
            session.Continue();
        });

        Assert.Equal(new[] { 5 }, lines);
        Assert.NotNull(captured);

        var next = captured.Single(v => v.Name == "a").Children.Single(c => c.Name == "Next");
        var backToA = next.Children.Single(c => c.Name == "Prev");
        Assert.Contains("circular reference", backToA.ValueDisplay);
        Assert.Empty(backToA.Children);

        var head = captured.Single(v => v.Name == "head");
        Assert.Equal("7", head.Children.Single(c => c.Name == "val").ValueDisplay);
        Assert.Equal("8", head.Children.Single(c => c.Name == "next").Children.Single(c => c.Name == "val").ValueDisplay);
    }

    // Helper declared below its first use, the usual script layout
    private const string RecursiveDepthScript = @"int total = Depth(3);
Console.WriteLine(total);

int Depth(int n)
{
    if (n == 0) return 0;
    return 1 + Depth(n - 1);
}";

    [Fact]
    public async Task DebugExecution_StepOverRecursiveCall_DoesNotStopInsideCallee()
    {
        var lines = await RunWithBreakpointsAsync(RecursiveDepthScript, new[] { 1 }, (session, line) =>
        {
            if (line == 1) session.StepOver();
            else session.Continue();
        });

        Assert.Equal(new[] { 1, 2 }, lines);
    }

    [Fact]
    public async Task DebugExecution_StepOverInsideRecursion_ReturnsToCallerFrame()
    {
        var lines = await RunWithBreakpointsAsync(RecursiveDepthScript, new[] { 7 }, (session, line) =>
        {
            if (line == 7)
            {
                session.Breakpoints.ForEach(b => b.IsEnabled = false);
                session.StepOver();
            }
            else
            {
                session.Continue();
            }
        });

        // Stepping over `return 1 + Depth(n - 1)` in the outermost call skips all nested calls
        Assert.Equal(new[] { 7, 2 }, lines);
    }

    [Fact]
    public async Task DebugExecution_BreakpointInRecursion_ReportsOneFramePerActiveCall()
    {
        var stacks = new List<IReadOnlyList<CallStackFrameItem>>();
        await RunWithBreakpointsAsync(RecursiveDepthScript, new[] { 6 }, (session, _) =>
        {
            stacks.Add(session.CallStack);
            session.Continue();
        });

        var deepest = stacks.OrderByDescending(s => s.Count).First();
        Assert.Equal(5, deepest.Count);
        Assert.Equal("Depth(int n)", deepest[0].MethodName);
        Assert.True(deepest[0].IsCurrentFrame);
        Assert.Equal(6, deepest[0].LineNumber);
        Assert.All(deepest.Skip(1).Take(3), frame =>
        {
            Assert.Equal("Depth(int n)", frame.MethodName);
            Assert.Equal(7, frame.LineNumber);
        });
        Assert.Equal("<Top-Level Statements>", deepest[^1].MethodName);
        Assert.Equal(1, deepest[^1].LineNumber);
    }

    [Fact]
    public async Task DebugExecution_MemoizedLocalFunction_ShowsCapturedMemoAndCompilesWhenResultAssignedFromCall()
    {
        var code = @"var memo = new Dictionary<int, long>();
long result = Fib(10);
Console.WriteLine(result);

long Fib(int n)
{
    if (n < 2) return n;
    if (memo.TryGetValue(n, out var cached)) return cached;
    return memo[n] = Fib(n - 1) + Fib(n - 2);
}";

        IReadOnlyList<DebugVariableItem>? insideFib = null;
        var lines = await RunWithBreakpointsAsync(code, new[] { 7 }, (session, _) =>
        {
            insideFib = session.CapturedLocals.ToList();
            session.Breakpoints.ForEach(b => b.IsEnabled = false);
            session.Continue();
        });

        Assert.Equal(new[] { 7 }, lines);
        Assert.NotNull(insideFib);
        Assert.Contains(insideFib, v => v.Name == "n");
        Assert.Contains(insideFib, v => v.Name == "memo");
        Assert.DoesNotContain(insideFib, v => v.Name == "result");
    }

    [Fact]
    public async Task DebugExecution_GetterEvaluatedDuringLocalsCapture_DoesNotPauseTwice()
    {
        var code = @"var holder = new Holder();
Console.WriteLine(holder.Value);

public class Holder
{
    public int Value
    {
        get
        {
            return 42;
        }
    }
}";

        IReadOnlyList<DebugVariableItem>? topLevelLocals = null;
        IReadOnlyList<CallStackFrameItem>? getterStack = null;
        var lines = await RunWithBreakpointsAsync(code, new[] { 2, 10 }, (session, line) =>
        {
            if (line == 2) topLevelLocals = session.CapturedLocals.ToList();
            if (line == 10) getterStack = session.CallStack;
            session.Continue();
        });

        Assert.Equal(new[] { 2, 10 }, lines);
        Assert.Equal("42", topLevelLocals?.Single(v => v.Name == "holder").Children.Single(c => c.Name == "Value").ValueDisplay);
        Assert.NotNull(getterStack);
        Assert.Equal(new[] { "Holder.Value.get", "<Top-Level Statements>" }, getterStack.Select(f => f.MethodName));
        Assert.Equal(2, getterStack[1].LineNumber);
    }

    [Fact]
    public async Task DebugExecution_EveryKindOfCallableBody_InstrumentsAndRunsCorrectly()
    {
        var code = @"var items = new List<int> { 3, 1, 2 };
items.ForEach(x => { Console.WriteLine(x); });
Func<int, int> twice = delegate (int v) { return v * 2; };
Console.WriteLine(Evens(10).Count() + twice(2) + await Delay() + new Vec(1, 2).Length);
var v2 = new Vec(1, 1) + new Vec(2, 2);
Console.WriteLine(Generic.Max(3, 7) + v2.X + (int)v2 + new Bag()[0] + new Point(1, 2).Sum() + Square(2));

static IEnumerable<int> Evens(int limit)
{
    for (int i = 0; i < limit; i += 2) yield return i;
}

async Task<int> Delay()
{
    await Task.Yield();
    return 1;
}

int Square(int x) => x * x;

public struct Vec
{
    public Vec(int x, int y) { X = x; Y = y; }
    public int X { get; }
    public int Y { get; }
    public int Length { get { return X + Y; } }
    public static Vec operator +(Vec a, Vec b) { return new Vec(a.X + b.X, a.Y + b.Y); }
    public static explicit operator int(Vec v) { return v.X; }
}

public class Bag
{
    private readonly int[] _data = { 5 };
    public int this[int i] { get { return _data[i]; } set { _data[i] = value; } }
    public event Action? Changed { add { } remove { } }
}

public record Point(int X, int Y)
{
    public int Sum() { return X + Y; }
}

public static class Generic
{
    public static T Max<T>(T a, T b) where T : IComparable<T>
    {
        start:
        if (a.CompareTo(b) < 0) { (a, b) = (b, a); goto start; }
        return a;
    }
}";

        var (success, bytes, diagnostics) = CreateDebugger().CompileForDebugging(code, ExecutionLanguageMode.Statements);
        Assert.True(success, string.Join("; ", diagnostics.Select(d => $"{d.Id} L{d.Line}: {d.Message}")));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ScriptDebugSession.BeginSession(Array.Empty<BreakpointItem>(), cts, "callable-bodies");
        var output = new System.Text.StringBuilder();
        try
        {
            var result = await _engine.ExecuteAsync(bytes!, text => output.Append(text), cts.Token);
            Assert.True(result.Success, result.Error);
        }
        finally
        {
            ScriptDebugSession.EndSession();
        }

        var printed = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(new[] { "3", "1", "2", "13", "25" }, printed);
    }

    [Fact]
    public async Task DebugExecution_InstrumentedScript_KeepsOriginalLineNumbers()
    {
        var code = @"Console.WriteLine(Where());
var list = new List<int> { 1 };
if (list.Count > 0)
    Console.WriteLine(Where());
foreach (var x in list)
{
    Console.WriteLine(Where());
}

int Where([System.Runtime.CompilerServices.CallerLineNumber] int line = 0) => line;";

        var (success, bytes, diagnostics) = CreateDebugger().CompileForDebugging(code, ExecutionLanguageMode.Statements);
        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ScriptDebugSession.BeginSession(Array.Empty<BreakpointItem>(), cts, "line-numbers");
        var output = new System.Text.StringBuilder();
        try
        {
            var result = await _engine.ExecuteAsync(bytes!, text => output.Append(text), cts.Token);
            Assert.True(result.Success, result.Error);
        }
        finally
        {
            ScriptDebugSession.EndSession();
        }

        var printed = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(new[] { "1", "4", "7" }, printed);
    }

    private async Task<List<int>> RunWithBreakpointsAsync(string code, int[] breakpointLines, Action<ScriptDebugSession, int> onPaused)
    {
        var (success, bytes, diagnostics) = CreateDebugger().CompileForDebugging(code, ExecutionLanguageMode.Statements);
        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var breakpoints = breakpointLines.Select(l => new BreakpointItem { LineNumber = l, IsEnabled = true });
        var session = ScriptDebugSession.BeginSession(breakpoints, cts, "debug-test");

        var pausedLines = new List<int>();
        session.Paused += (line, _) =>
        {
            pausedLines.Add(line);
            onPaused(session, line);
        };

        try
        {
            var result = await _engine.ExecuteAsync(bytes!, _ => { }, cts.Token);
            Assert.True(result.Success, result.Error);
            return pausedLines;
        }
        finally
        {
            ScriptDebugSession.EndSession();
        }
    }
}
