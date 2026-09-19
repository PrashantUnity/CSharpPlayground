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
}
