using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpCodeStudioViewModel
{
    [ObservableProperty]
    private bool _isDebugging;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _currentPausedLine = -1;

    [ObservableProperty]
    private string _immediateInputText = string.Empty;

    [ObservableProperty]
    private string _watchInputText = string.Empty;

    public ObservableCollection<BreakpointItem> Breakpoints { get; } = new();
    public ObservableCollection<DebugVariableItem> Locals { get; } = new();
    public ObservableCollection<WatchExpressionItem> WatchExpressions { get; } = new();
    public ObservableCollection<CallStackFrameItem> CallStack { get; } = new();
    public ObservableCollection<string> ImmediateOutput { get; } = new();

    public event Action<int>? RequestSetPausedLine;
    public event Action<IEnumerable<int>>? RequestSyncBreakpoints;

    partial void OnIsDebuggingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNormalExecuting));
    }

    [RelayCommand]
    public async Task DebugCodeAsync()
    {
        if (IsExecuting || IsDebugging) return;

        var debuggingTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);

        DisposeRichOutputControls();
        DumpResults.Clear();
        RichOutputs.Clear();
        Locals.Clear();
        CallStack.Clear();
        GlobalVariableCache.Clear();

        if (debuggingTab != null)
        {
            debuggingTab.DumpResults.Clear();
            debuggingTab.RichOutputs.Clear();
            debuggingTab.Locals.Clear();
            debuggingTab.CallStack.Clear();
            debuggingTab.IsExecuting = true;
            debuggingTab.IsDebugging = true;
            debuggingTab.IsPaused = false;
            debuggingTab.PausedLine = -1;
            debuggingTab.ConsoleOutput = "🐞 Starting interactive C# debugging session with active breakpoints...\n";
            debuggingTab.CompilerStatusText = "Compiling for Debug...";
        }

        SelectedBottomTabIndex = 4;
        IsBottomDeckExpanded = true;
        ConsoleOutput = "🐞 Starting interactive C# debugging session with active breakpoints...\n";
        CompilerStatusText = "Compiling for Debug...";
        IsExecuting = true;
        IsDebugging = true;
        IsPaused = false;
        CurrentPausedLine = -1;

        debuggingTab?.ExecutionCts?.Cancel();
        _executionCts?.Cancel();
        _executionCts = new CancellationTokenSource();
        if (debuggingTab != null) debuggingTab.ExecutionCts = _executionCts;
        var token = _executionCts.Token;

        var (compileOk, bytes, diagnostics) = await Task.Run(() =>
            _debuggerService.CompileForDebugging(Code, CurrentLanguageMode));

        if (!compileOk || bytes == null)
        {
            var failMsg = "❌ Debug compilation failed. Check the Problems tab for details.\n";
            if (debuggingTab != null)
            {
                debuggingTab.ConsoleOutput += failMsg;
                debuggingTab.Diagnostics.Clear();
                foreach (var d in diagnostics)
                {
                    debuggingTab.Diagnostics.Add(new DiagnosticItemViewModel(d, (l, c) => RequestNavigateToCaret?.Invoke(l, c)));
                }
                debuggingTab.CompilerStatusText = "Build Failed";
                debuggingTab.IsExecuting = false;
                debuggingTab.IsDebugging = false;
            }

            if (debuggingTab == null || debuggingTab.IsActive)
            {
                ConsoleOutput += failMsg;
                Diagnostics.Clear();
                foreach (var d in diagnostics)
                {
                    Diagnostics.Add(new DiagnosticItemViewModel(d, (l, c) => RequestNavigateToCaret?.Invoke(l, c)));
                }
                ErrorCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
                WarningCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
                SelectedBottomTabIndex = 2;
                CompilerStatusText = "Build Failed";
                IsExecuting = false;
                IsDebugging = false;
            }
            return;
        }

        var readyMsg = "✨ Instrumentation ready! Executing in-memory...\n--------------------------------------------------\n";
        if (debuggingTab != null)
        {
            debuggingTab.ConsoleOutput += readyMsg;
            debuggingTab.CompilerStatusText = "Debugging...";
        }
        if (debuggingTab == null || debuggingTab.IsActive)
        {
            ConsoleOutput += readyMsg;
            CompilerStatusText = "Debugging...";
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var session = ScriptDebugSession.BeginSession(Breakpoints, _executionCts, Script.Id);

            session.Paused += (line, locals) =>
            {
                if (debuggingTab != null)
                {
                    debuggingTab.IsPaused = true;
                    debuggingTab.PausedLine = line;
                    debuggingTab.CompilerStatusText = $"⏸️ Paused at Line {line} (Breakpoint)";
                    debuggingTab.Locals.Clear();
                    foreach (var l in locals) debuggingTab.Locals.Add(l);
                    debuggingTab.CallStack.Clear();
                    debuggingTab.CallStack.Add(new CallStackFrameItem
                    {
                        FrameIndex = 0,
                        MethodName = CurrentLanguageMode == ExecutionLanguageMode.Program ? "Main()" : "<Top-Level Statements>",
                        LineNumber = line,
                        FileName = Script.Title.EndsWith(".cs") ? Script.Title : $"{Script.Title}.cs",
                        IsCurrentFrame = true
                    });
                }

                if (debuggingTab == null || debuggingTab.IsActive)
                {
                    IsPaused = true;
                    CurrentPausedLine = line;
                    CompilerStatusText = $"⏸️ Paused at Line {line} (Breakpoint)";

                    Locals.Clear();
                    foreach (var l in locals)
                    {
                        Locals.Add(l);
                    }

                    CallStack.Clear();
                    CallStack.Add(new CallStackFrameItem
                    {
                        FrameIndex = 0,
                        MethodName = CurrentLanguageMode == ExecutionLanguageMode.Program ? "Main()" : "<Top-Level Statements>",
                        LineNumber = line,
                        FileName = Script.Title.EndsWith(".cs") ? Script.Title : $"{Script.Title}.cs",
                        IsCurrentFrame = true
                    });

                    _ = UpdateWatchExpressionsAsync();

                    SelectedBottomTabIndex = 4;
                    IsBottomDeckExpanded = true;

                    RequestSetPausedLine?.Invoke(line);
                    RequestNavigateToCaret?.Invoke(line, 1);
                }
            };

            session.Resumed += () =>
            {
                if (debuggingTab != null)
                {
                    debuggingTab.IsPaused = false;
                    debuggingTab.PausedLine = -1;
                    debuggingTab.CompilerStatusText = "Debugging...";
                }

                if (debuggingTab == null || debuggingTab.IsActive)
                {
                    IsPaused = false;
                    CurrentPausedLine = -1;
                    CompilerStatusText = "Debugging...";
                    RequestSetPausedLine?.Invoke(-1);
                }
            };

            session.Stopped += () =>
            {
                if (debuggingTab != null)
                {
                    debuggingTab.IsPaused = false;
                    debuggingTab.PausedLine = -1;
                }

                if (debuggingTab == null || debuggingTab.IsActive)
                {
                    IsPaused = false;
                    CurrentPausedLine = -1;
                    RequestSetPausedLine?.Invoke(-1);
                }
            };

            var result = await _executionEngine.ExecuteAsync(
                bytes,
                liveText =>
                {
                    Action appendDebugOutput = () =>
                    {
                        if (debuggingTab != null)
                        {
                            debuggingTab.ConsoleOutput += liveText;
                            if (debuggingTab.IsActive)
                            {
                                ConsoleOutput = debuggingTab.ConsoleOutput;
                            }
                        }
                        else
                        {
                            ConsoleOutput += liveText;
                        }
                    };

                    if (Avalonia.Application.Current != null && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(appendDebugOutput);
                    }
                    else
                    {
                        appendDebugOutput();
                    }
                },
                token);

            sw.Stop();
            var timeText = $"{sw.Elapsed.TotalMilliseconds:N0} ms";
            var endBorder = "\n--------------------------------------------------\n";

            if (result.Success)
            {
                var successMsg = $"{endBorder}🏁 Debugging finished in {sw.Elapsed.TotalMilliseconds:N0} ms\n";
                var statusText = DumpResults.Count > 0
                    ? $"Completed • {DumpResults.Count} visual dump{(DumpResults.Count == 1 ? "" : "s")}"
                    : "Completed";
                Script.ExecutionCount++;
                _ = _storageService.SaveScriptAsync(Script);

                if (debuggingTab != null)
                {
                    debuggingTab.ConsoleOutput += successMsg;
                    debuggingTab.ExecutionTimeText = timeText;
                    debuggingTab.CompilerStatusText = statusText;
                }
                if (debuggingTab == null || debuggingTab.IsActive)
                {
                    ConsoleOutput += successMsg;
                    ExecutionTimeText = timeText;
                    CompilerStatusText = statusText;
                }
            }
            else if (result.WasCancelled)
            {
                var cancelMsg = $"{endBorder}🛑 Debug session stopped by user.\n";
                if (debuggingTab != null)
                {
                    debuggingTab.ConsoleOutput += cancelMsg;
                    debuggingTab.CompilerStatusText = "Stopped";
                }
                if (debuggingTab == null || debuggingTab.IsActive)
                {
                    ConsoleOutput += cancelMsg;
                    CompilerStatusText = "Stopped";
                }
            }
            else
            {
                var errorMsg = $"{endBorder}❌ Runtime Error: {result.Error}\n";
                if (debuggingTab != null)
                {
                    debuggingTab.ConsoleOutput += errorMsg;
                    debuggingTab.CompilerStatusText = "Runtime Error";
                }
                if (debuggingTab == null || debuggingTab.IsActive)
                {
                    ConsoleOutput += errorMsg;
                    CompilerStatusText = "Runtime Error";
                }
            }
        }
        finally
        {
            ScriptDebugSession.EndSession();
            GlobalVariableCache.Clear();

            if (debuggingTab != null)
            {
                debuggingTab.IsExecuting = false;
                debuggingTab.IsDebugging = false;
                debuggingTab.IsPaused = false;
                debuggingTab.PausedLine = -1;
            }

            if (debuggingTab == null || debuggingTab.IsActive)
            {
                IsExecuting = false;
                IsDebugging = false;
                IsPaused = false;
                CurrentPausedLine = -1;
                RequestSetPausedLine?.Invoke(-1);
            }
        }
    }

    [RelayCommand]
    public void ContinueDebug()
    {
        ScriptDebugSession.Current?.Continue();
    }

    [RelayCommand]
    public void StepOver()
    {
        ScriptDebugSession.Current?.StepOver();
    }

    [RelayCommand]
    public void StepInto()
    {
        ScriptDebugSession.Current?.StepInto();
    }

    [RelayCommand]
    public void StopDebug()
    {
        var targetTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        targetTab?.ExecutionCts?.Cancel();
        ScriptDebugSession.Current?.Stop();
        _executionCts?.Cancel();

        if (targetTab != null)
        {
            targetTab.IsDebugging = false;
            targetTab.IsExecuting = false;
            targetTab.IsPaused = false;
            targetTab.PausedLine = -1;
            targetTab.ConsoleOutput += "\n🛑 Debug session stopped by user.\n";
            targetTab.CompilerStatusText = "Stopped";
        }

        if (targetTab == null || targetTab.IsActive)
        {
            IsDebugging = false;
            IsExecuting = false;
            IsPaused = false;
            CurrentPausedLine = -1;
            RequestSetPausedLine?.Invoke(-1);
            ConsoleOutput += "\n🛑 Debug session stopped by user.\n";
            CompilerStatusText = "Stopped";
        }
    }

    [RelayCommand]
    public async Task RestartDebugAsync()
    {
        StopDebug();
        await Task.Delay(200);
        await DebugCodeAsync();
    }

    [RelayCommand]
    public void ToggleBreakpoint(int line)
    {
        var existing = Breakpoints.FirstOrDefault(b => b.LineNumber == line);
        if (existing != null)
        {
            Breakpoints.Remove(existing);
            Script.Breakpoints.Remove(line);
        }
        else
        {
            var bp = new BreakpointItem { LineNumber = line, IsEnabled = true };
            Breakpoints.Add(bp);
            if (!Script.Breakpoints.Contains(line))
            {
                Script.Breakpoints.Add(line);
            }
        }

        var sorted = Breakpoints.OrderBy(b => b.LineNumber).ToList();
        Breakpoints.Clear();
        foreach (var b in sorted)
        {
            Breakpoints.Add(b);
        }

        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
        _ = _storageService.SaveScriptAsync(Script);
    }

    [RelayCommand]
    public void RemoveBreakpoint(BreakpointItem? item)
    {
        if (item == null) return;
        Breakpoints.Remove(item);
        Script.Breakpoints.Remove(item.LineNumber);
        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
        _ = _storageService.SaveScriptAsync(Script);
    }

    [RelayCommand]
    public void ClearAllBreakpoints()
    {
        Breakpoints.Clear();
        Script.Breakpoints.Clear();
        RequestSyncBreakpoints?.Invoke(Array.Empty<int>());
        _ = _storageService.SaveScriptAsync(Script);
    }

    [RelayCommand]
    public void ToggleBreakpointEnabled(BreakpointItem? item)
    {
        if (item == null) return;
        item.IsEnabled = !item.IsEnabled;
        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
    }

    [RelayCommand]
    public async Task AddWatchAsync()
    {
        if (string.IsNullOrWhiteSpace(WatchInputText)) return;
        var expr = WatchInputText.Trim();
        WatchInputText = string.Empty;

        var watchItem = new WatchExpressionItem
        {
            Expression = expr,
            Result = "Evaluating...",
            TypeName = ""
        };
        WatchExpressions.Add(watchItem);

        var (ok, res, type) = await _debuggerService.EvaluateExpressionAsync(expr, Locals.ToList());
        watchItem.Result = res;
        watchItem.TypeName = type;
        watchItem.HasError = !ok;
    }

    [RelayCommand]
    public async Task AddWatchExpressionAsync(string? expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return;
        expr = expr.Trim();
        if (WatchExpressions.Any(w => w.Expression == expr)) return;

        var watchItem = new WatchExpressionItem
        {
            Expression = expr,
            Result = "Evaluating...",
            TypeName = ""
        };
        WatchExpressions.Add(watchItem);

        var (ok, res, type) = await _debuggerService.EvaluateExpressionAsync(expr, Locals.ToList());
        watchItem.Result = res;
        watchItem.TypeName = type;
        watchItem.HasError = !ok;

        SelectedBottomTabIndex = 4;
        IsBottomDeckExpanded = true;
    }

    public Task<(bool Success, string Result, string TypeName)> EvaluateExpressionAsync(string expr)
    {
        return _debuggerService.EvaluateExpressionAsync(expr, Locals.ToList());
    }

    [RelayCommand]
    public void RemoveWatch(WatchExpressionItem? item)
    {
        if (item == null) return;
        WatchExpressions.Remove(item);
    }

    private async Task UpdateWatchExpressionsAsync()
    {
        var localsSnapshot = Locals.ToList();
        foreach (var w in WatchExpressions)
        {
            var (ok, res, type) = await _debuggerService.EvaluateExpressionAsync(w.Expression, localsSnapshot);
            w.Result = res;
            w.TypeName = type;
            w.HasError = !ok;
        }
    }

    [RelayCommand]
    public async Task EvaluateImmediateAsync()
    {
        if (string.IsNullOrWhiteSpace(ImmediateInputText)) return;
        var expr = ImmediateInputText.Trim();
        ImmediateInputText = string.Empty;

        ImmediateOutput.Add($"> {expr}");
        var (ok, res, type) = await _debuggerService.EvaluateExpressionAsync(expr, Locals.ToList());
        if (ok)
        {
            ImmediateOutput.Add($"  {res} ({type})");
        }
        else
        {
            ImmediateOutput.Add($"  ❌ Error: {res}");
        }
    }

    [RelayCommand]
    public void ClearImmediate()
    {
        ImmediateOutput.Clear();
    }
}
