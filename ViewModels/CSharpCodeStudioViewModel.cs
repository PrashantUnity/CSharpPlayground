using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpCodeStudioViewModel : ObservableObject
{
    private readonly IScriptStorageService _storageService;
    private readonly RoslynCompilerService _compilerService;
    public RoslynCompilerService CompilerService => _compilerService;
    private readonly ScriptExecutionEngine _executionEngine;
    private readonly ScriptDebuggerService _debuggerService;
    private readonly NotebookExecutionKernel _kernel;
    private readonly Action _backToHubAction;
    private readonly Action? _backToHomeAction;

    private CancellationTokenSource? _diagnosticsCts;
    private CancellationTokenSource? _executionCts;
    private readonly Func<int> _getTimeoutSeconds;

    [ObservableProperty]
    private ScriptDocumentItem _script;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private int _selectedLeftTabIndex = 0; // 0 = Description & Notes, 1 = References, 2 = Testcases

    [ObservableProperty]
    private int _selectedBottomTabIndex = 0; // 0 = Results (.Dump), 1 = Console Output, 2 = Problems, 3 = Test Cases

    [ObservableProperty]
    private bool _isBottomDeckExpanded = true;

    [ObservableProperty]
    private int _selectedLanguageModeIndex = 0;

    [ObservableProperty]
    private bool _isExecuting;

    partial void OnIsExecutingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNormalExecuting));
    }

    partial void OnIsDebuggingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNormalExecuting));
    }

    public bool IsNormalExecuting => IsExecuting && !IsDebugging;

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private string _compilerStatusText = "Ready";

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private string _consoleOutput = string.Empty;

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    public ObservableCollection<DumpTableResult> DumpResults { get; } = new();
    public ObservableCollection<RichCellOutput> RichOutputs { get; } = new();
    public ObservableCollection<DiagnosticItemViewModel> Diagnostics { get; } = new();
    public ObservableCollection<AssemblyReferenceViewModel> References { get; } = new();
    public ObservableCollection<TestCaseItem> TestCases { get; } = new();

    // --- Interactive Debugging State & Collections ---
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

    public ObservableCollection<string> LanguageModes { get; } = new()
    {
        "C# Statements",
        "C# Program (Main)",
        "C# Expression"
    };

    public event Action<int, int>? RequestNavigateToCaret;
    public event Action? RequestFoldAll;
    public event Action? RequestUnfoldAll;
    public event Action? RequestToggleSearch;

    [ObservableProperty]
    private bool _isWordWrap;

    [RelayCommand]
    public void FoldAll() => RequestFoldAll?.Invoke();

    [RelayCommand]
    public void UnfoldAll() => RequestUnfoldAll?.Invoke();

    [RelayCommand]
    public void ToggleSearch() => RequestToggleSearch?.Invoke();

    [RelayCommand]
    public void ToggleWordWrap() => IsWordWrap = !IsWordWrap;

    [RelayCommand]
    public void FormatCode()
    {
        if (string.IsNullOrWhiteSpace(Code)) return;
        try
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(Code);
            var root = tree.GetRoot();
            Code = Microsoft.CodeAnalysis.SyntaxNodeExtensions.NormalizeWhitespace(root).ToFullString();
        }
        catch
        {
            // Ignore format errors if code has syntax errors
        }
    }

    public ExecutionLanguageMode CurrentLanguageMode => SelectedLanguageModeIndex switch
    {
        1 => ExecutionLanguageMode.Program,
        2 => ExecutionLanguageMode.Expression,
        _ => ExecutionLanguageMode.Statements
    };

    public CSharpCodeStudioViewModel(
        ScriptDocumentItem script,
        IScriptStorageService storageService,
        RoslynCompilerService compilerService,
        ScriptExecutionEngine executionEngine,
        Action backToHubAction,
        Action? backToHomeAction = null,
        Func<int>? getTimeoutSeconds = null)
    {
        _script = script;
        _storageService = storageService;
        _compilerService = compilerService;
        _executionEngine = executionEngine;
        _debuggerService = new ScriptDebuggerService(_compilerService, _executionEngine);
        _backToHubAction = backToHubAction;
        _backToHomeAction = backToHomeAction;
        _getTimeoutSeconds = getTimeoutSeconds ?? (() => 10);
        _kernel = new NotebookExecutionKernel();

        _code = script.Code;
        _notes = script.Notes;
        _selectedLanguageModeIndex = script.ExecutionMode switch
        {
            "Program" => 1,
            "Expression" => 2,
            _ => 0
        };

        foreach (var r in _compilerService.AvailableReferences)
        {
            References.Add(new AssemblyReferenceViewModel(r));
        }

        foreach (var tc in script.TestCases)
        {
            TestCases.Add(tc);
        }

        if (TestCases.Count == 0)
        {
            TestCases.Add(new TestCaseItem { Name = "Case 1", Input = "// Sample input parameters" });
        }

        Breakpoints.Clear();
        foreach (var bpLine in script.Breakpoints)
        {
            Breakpoints.Add(new BreakpointItem { LineNumber = bpLine, IsEnabled = true });
        }

        TriggerDiagnosticsCheck();
    }

    public void UpdateActiveScript(ScriptDocumentItem script)
    {
        Script = script;
        Code = script.Code;
        Notes = script.Notes;
        SelectedLanguageModeIndex = script.ExecutionMode switch
        {
            "Program" => 1,
            "Expression" => 2,
            _ => 0
        };

        ConsoleOutput = string.Empty;
        ExecutionTimeText = string.Empty;

        TestCases.Clear();
        foreach (var tc in script.TestCases)
        {
            TestCases.Add(tc);
        }

        Breakpoints.Clear();
        foreach (var bpLine in script.Breakpoints)
        {
            Breakpoints.Add(new BreakpointItem { LineNumber = bpLine, IsEnabled = true });
        }

        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));

        TriggerDiagnosticsCheck();
    }

    partial void OnCodeChanged(string value)
    {
        Script.Code = value;
        Script.LastModified = DateTime.UtcNow;
        TriggerDiagnosticsCheck();
    }

    partial void OnNotesChanged(string value)
    {
        Script.Notes = value;
        Script.LastModified = DateTime.UtcNow;
    }

    partial void OnSelectedLanguageModeIndexChanged(int value)
    {
        Script.ExecutionMode = value switch
        {
            1 => "Program",
            2 => "Expression",
            _ => "Statements"
        };
        TriggerDiagnosticsCheck();
    }

    private void TriggerDiagnosticsCheck()
    {
        _diagnosticsCts?.Cancel();
        _diagnosticsCts = new CancellationTokenSource();
        var token = _diagnosticsCts.Token;

        var codeSnapshot = Code;
        var mode = CurrentLanguageMode;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;

                CompilerStatusText = "Analyzing...";
                var items = _compilerService.CheckDiagnostics(codeSnapshot, mode);

                if (token.IsCancellationRequested) return;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Diagnostics.Clear();
                    foreach (var item in items)
                    {
                        Diagnostics.Add(new DiagnosticItemViewModel(item, (l, c) =>
                        {
                            RequestNavigateToCaret?.Invoke(l, c);
                        }));
                    }

                    ErrorCount = items.Count(i => i.Severity == DiagnosticSeverity.Error);
                    WarningCount = items.Count(i => i.Severity == DiagnosticSeverity.Warning);

                    if (ErrorCount > 0)
                    {
                        CompilerStatusText = $"{ErrorCount} Error{(ErrorCount > 1 ? "s" : "")}";
                    }
                    else if (WarningCount > 0)
                    {
                        CompilerStatusText = $"{WarningCount} Warning{(WarningCount > 1 ? "s" : "")}";
                    }
                    else
                    {
                        CompilerStatusText = "Ready";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Debouncing
            }
        }, token);
    }

    /// <summary>Disposes any live Control output (e.g. a Display.Animate control's timer) before
    /// RichOutputs is cleared — without this, re-running or clearing results leaks a running timer for
    /// every animated control that was ever displayed in this tab.</summary>
    private void DisposeRichOutputControls()
    {
        foreach (var output in RichOutputs)
        {
            InteractiveControlLifecycle.DisposeIfNeeded(output.InteractiveControl);
        }
    }

    [RelayCommand]
    public void ClearResults()
    {
        DisposeRichOutputControls();
        DumpResults.Clear();
        RichOutputs.Clear();
    }

    [RelayCommand]
    public async Task CopyTableTsvAsync(DumpTableResult? table)
    {
        if (table == null) return;
        var text = table.ToTsv();
        await SetClipboardTextAsync(text);
        CompilerStatusText = $"Copied {table.FullHeaderTitle} as TSV to clipboard";
    }

    [RelayCommand]
    public async Task CopyTableCsvAsync(DumpTableResult? table)
    {
        if (table == null) return;
        var text = table.ToCsv();
        await SetClipboardTextAsync(text);
        CompilerStatusText = $"Copied {table.FullHeaderTitle} as CSV to clipboard";
    }

    [RelayCommand]
    public async Task CopyTableJsonAsync(DumpTableResult? table)
    {
        if (table == null) return;
        var text = table.ToJson();
        await SetClipboardTextAsync(text);
        CompilerStatusText = $"Copied {table.FullHeaderTitle} as JSON to clipboard";
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        }
    }

    [RelayCommand]
    private async Task RunCodeAsync()
    {
        if (IsExecuting) return;

        DisposeRichOutputControls();
        DumpResults.Clear();
        RichOutputs.Clear();
        SelectedBottomTabIndex = 0; // Default to Results
        IsBottomDeckExpanded = true;
        ConsoleOutput = "🚀 Running C# code (.Dump enabled)...\n";
        CompilerStatusText = "Executing...";
        IsExecuting = true;

        _executionCts?.Cancel();
        _executionCts = new CancellationTokenSource();
        var timeoutSeconds = Math.Max(1, _getTimeoutSeconds());
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_executionCts.Token, timeoutCts.Token);
        var token = linkedCts.Token;

        using var scope = InteractiveDisplayContext.EnterScope(richOutput =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RichOutputs.Add(richOutput);
                if (richOutput.TableResult != null)
                {
                    DumpResults.Add(richOutput.TableResult);
                    SelectedBottomTabIndex = 0; // Automatically show Results tab
                }
            });
        });
        // Also covers "Program" mode below: ScriptExecutionEngine has no cancellation-context scope of
        // its own, so this outer scope is the only place compiled Main() code can observe Display.
        // CancellationToken/ThrowIfCancellationRequested() at all.
        using var cancellationScope = InteractiveCancellationContext.EnterScope(token);

        try
        {
            if (CurrentLanguageMode == ExecutionLanguageMode.Statements || CurrentLanguageMode == ExecutionLanguageMode.Expression)
            {
                // Execute via Roslyn Scripting Kernel (Supports top-level statements, collection expressions, #r nuget, .Dump)
                _kernel.ResetSession();

                var codeToRun = Code;
                if (CurrentLanguageMode == ExecutionLanguageMode.Expression)
                {
                    var expr = Code.Trim().TrimEnd(';');
                    codeToRun = $"({expr}).Dump();";
                }

                var kernelResult = await _kernel.ExecuteCellAsync(
                    codeToRun,
                    ct: token,
                    onLiveConsole: liveText =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            ConsoleOutput += liveText;
                        });
                    },
                    onRichOutput: rich =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            RichOutputs.Add(rich);
                            if (rich.TableResult != null)
                            {
                                DumpResults.Add(rich.TableResult);
                                SelectedBottomTabIndex = 0;
                            }
                        });
                    });

                if (kernelResult.Success)
                {
                    ExecutionTimeText = $"{kernelResult.Elapsed.TotalMilliseconds:N0} ms";
                    CompilerStatusText = DumpResults.Count > 0
                        ? $"Completed • {DumpResults.Count} visual dump{(DumpResults.Count == 1 ? "" : "s")}"
                        : "Completed";
                    Script.ExecutionCount++;
                    _ = _storageService.SaveScriptAsync(Script);

                    if (DumpResults.Count > 0)
                    {
                        SelectedBottomTabIndex = 0;
                    }
                    else
                    {
                        SelectedBottomTabIndex = 1;
                    }
                }
                else if (kernelResult.WasCancelled)
                {
                    CompilerStatusText = timeoutCts.IsCancellationRequested
                        ? $"⏱️ Timed out after {timeoutSeconds}s"
                        : "🛑 Cancelled";
                }
                else
                {
                    CompilerStatusText = "Execution Failed";
                    if (kernelResult.Diagnostics.Count > 0)
                    {
                        Diagnostics.Clear();
                        foreach (var d in kernelResult.Diagnostics)
                        {
                            Diagnostics.Add(new DiagnosticItemViewModel(d, (l, c) => RequestNavigateToCaret?.Invoke(l, c)));
                        }
                        ErrorCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
                        WarningCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
                        SelectedBottomTabIndex = 2; // Problems tab
                    }
                    else
                    {
                        SelectedBottomTabIndex = 1; // Console Output
                    }
                }
            }
            else
            {
                // Program mode: compile full class/Main to assembly
                var (success, bytes, diagnostics) = await Task.Run(() =>
                    _compilerService.CompileToAssembly(Code, CurrentLanguageMode));

                if (!success || bytes == null)
                {
                    ConsoleOutput += "❌ Compilation failed. Check the Problems tab for details.\n";
                    foreach (var diag in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        ConsoleOutput += $"  • {diag.LocationString}: {diag.Id} {diag.Message}\n";
                    }
                    CompilerStatusText = "Build Failed";
                    SelectedBottomTabIndex = 2; // Jump to Problems
                    return;
                }

                ConsoleOutput += "✨ Build succeeded! Executing in-memory...\n";
                ConsoleOutput += "--------------------------------------------------\n";
                CompilerStatusText = "Running...";

                var result = await _executionEngine.ExecuteAsync(
                    bytes,
                    liveText =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            ConsoleOutput += liveText;
                        });
                    },
                    token);

                ConsoleOutput += "\n--------------------------------------------------\n";
                if (result.Success)
                {
                    ConsoleOutput += $"✅ Execution finished in {result.Elapsed.TotalMilliseconds:N0} ms\n";
                    ExecutionTimeText = $"{result.Elapsed.TotalMilliseconds:N0} ms";
                    CompilerStatusText = DumpResults.Count > 0
                        ? $"Completed • {DumpResults.Count} visual dump{(DumpResults.Count == 1 ? "" : "s")}"
                        : "Completed";
                    Script.ExecutionCount++;
                    _ = _storageService.SaveScriptAsync(Script);

                    if (DumpResults.Count > 0)
                    {
                        SelectedBottomTabIndex = 0;
                    }
                    else
                    {
                        SelectedBottomTabIndex = 1;
                    }
                }
                else if (result.WasCancelled)
                {
                    if (timeoutCts.IsCancellationRequested)
                    {
                        ConsoleOutput += $"⏱️ Execution timed out after {timeoutSeconds}s.\n";
                        CompilerStatusText = $"⏱️ Timed out after {timeoutSeconds}s";
                    }
                    else
                    {
                        ConsoleOutput += "⚠️ Execution was cancelled.\n";
                        CompilerStatusText = "🛑 Cancelled";
                    }
                }
                else
                {
                    ConsoleOutput += $"❌ Execution failed: {result.Error}\n";
                    CompilerStatusText = "Runtime Error";
                }
            }
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    private async Task RunTestCaseAsync(TestCaseItem testCase)
    {
        testCase.IsRunning = true;
        testCase.Passed = null;
        testCase.ActualOutput = "Running...";

        try
        {
            // Execute script and check output
            await RunCodeAsync();
            testCase.ActualOutput = ConsoleOutput;
            if (!string.IsNullOrEmpty(testCase.ExpectedOutput) && ConsoleOutput.Contains(testCase.ExpectedOutput))
            {
                testCase.Passed = true;
            }
            else
            {
                testCase.Passed = false;
            }
        }
        finally
        {
            testCase.IsRunning = false;
        }
    }

    [RelayCommand]
    private void AddTestCase()
    {
        var nextNum = TestCases.Count + 1;
        var newCase = new TestCaseItem
        {
            Name = $"Case {nextNum}",
            Input = $"// Input for Case {nextNum}"
        };
        TestCases.Add(newCase);
        Script.TestCases.Add(newCase);
    }

    [RelayCommand]
    private void Stop()
    {
        if (!IsExecuting) return;
        _executionCts?.Cancel();
        ConsoleOutput += "\n🛑 Cancellation requested by user...\n";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Script.Code = Code;
        Script.Notes = Notes;
        Script.TestCases = TestCases.ToList();
        Script.LastModified = DateTime.UtcNow;
        var saved = await _storageService.SaveScriptAsync(Script);
        CompilerStatusText = saved ? "Saved" : "⚠️ Save failed — check disk space/permissions";
    }

    [RelayCommand]
    private void BackToHub()
    {
        _ = SaveAsync();
        _backToHubAction.Invoke();
    }

    [RelayCommand]
    private void BackToHome()
    {
        _ = SaveAsync();
        _backToHomeAction?.Invoke();
    }

    [RelayCommand]
    private void ClearConsole()
    {
        ConsoleOutput = string.Empty;
    }

    [RelayCommand]
    private void SetLeftTab(string index)
    {
        if (int.TryParse(index, out var idx))
        {
            SelectedLeftTabIndex = idx;
        }
    }

    [RelayCommand]
    private void SetBottomTab(string index)
    {
        if (int.TryParse(index, out var idx))
        {
            SelectedBottomTabIndex = idx;
            IsBottomDeckExpanded = true;
        }
    }

    [RelayCommand]
    private void ToggleBottomDeck()
    {
        IsBottomDeckExpanded = !IsBottomDeckExpanded;
    }

    public void SetCaretPosition(int line, int col)
    {
        CaretLine = line;
        CaretColumn = col;
    }

    // =========================================================================
    // INTERACTIVE DEBUGGING COMMANDS & LOGIC
    // =========================================================================

    [RelayCommand]
    public async Task DebugCodeAsync()
    {
        if (IsExecuting || IsDebugging) return;

        DisposeRichOutputControls();
        DumpResults.Clear();
        RichOutputs.Clear();
        Locals.Clear();
        CallStack.Clear();
        GlobalVariableCache.Clear(); // Guarantee a clean slate — a prior session's stale values must never leak into this one
        SelectedBottomTabIndex = 4; // Automatically focus Debugger tab
        IsBottomDeckExpanded = true;
        ConsoleOutput = "🐞 Starting interactive C# debugging session with active breakpoints...\n";
        CompilerStatusText = "Compiling for Debug...";
        IsExecuting = true;
        IsDebugging = true;
        IsPaused = false;
        CurrentPausedLine = -1;

        _executionCts?.Cancel();
        _executionCts = new CancellationTokenSource();
        var token = _executionCts.Token;

        var (compileOk, bytes, diagnostics) = await Task.Run(() =>
            _debuggerService.CompileForDebugging(Code, CurrentLanguageMode));

        if (!compileOk || bytes == null)
        {
            ConsoleOutput += "❌ Debug compilation failed. Check the Problems tab for details.\n";
            Diagnostics.Clear();
            foreach (var d in diagnostics)
            {
                Diagnostics.Add(new DiagnosticItemViewModel(d, (l, c) => RequestNavigateToCaret?.Invoke(l, c)));
            }
            ErrorCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            WarningCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
            SelectedBottomTabIndex = 2; // Problems tab
            CompilerStatusText = "Build Failed";
            IsExecuting = false;
            IsDebugging = false;
            return;
        }

        ConsoleOutput += "✨ Instrumentation ready! Executing in-memory...\n";
        ConsoleOutput += "--------------------------------------------------\n";
        CompilerStatusText = "Debugging...";

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var session = ScriptDebugSession.BeginSession(Breakpoints, _executionCts);

            session.Paused += (line, locals) =>
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

                SelectedBottomTabIndex = 4; // Ensure Debugger tab is active
                IsBottomDeckExpanded = true;

                RequestSetPausedLine?.Invoke(line);
                RequestNavigateToCaret?.Invoke(line, 1);
            };

            session.Resumed += () =>
            {
                IsPaused = false;
                CurrentPausedLine = -1;
                CompilerStatusText = "Debugging...";
                RequestSetPausedLine?.Invoke(-1);
            };

            session.Stopped += () =>
            {
                IsPaused = false;
                CurrentPausedLine = -1;
                RequestSetPausedLine?.Invoke(-1);
            };

            var result = await _executionEngine.ExecuteAsync(
                bytes,
                liveText =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        ConsoleOutput += liveText;
                    });
                },
                token);

            sw.Stop();
            ExecutionTimeText = $"{sw.Elapsed.TotalMilliseconds:N0} ms";

            ConsoleOutput += "\n--------------------------------------------------\n";
            if (result.Success)
            {
                ConsoleOutput += $"🏁 Debugging finished in {sw.Elapsed.TotalMilliseconds:N0} ms\n";
                CompilerStatusText = DumpResults.Count > 0
                    ? $"Completed • {DumpResults.Count} visual dump{(DumpResults.Count == 1 ? "" : "s")}"
                    : "Completed";
                Script.ExecutionCount++;
                _ = _storageService.SaveScriptAsync(Script);
            }
            else if (result.WasCancelled)
            {
                ConsoleOutput += "🛑 Debug session stopped by user.\n";
                CompilerStatusText = "Stopped";
            }
            else
            {
                ConsoleOutput += $"❌ Runtime Error: {result.Error}\n";
                CompilerStatusText = "Runtime Error";
            }
        }
        finally
        {
            ScriptDebugSession.EndSession();
            GlobalVariableCache.Clear(); // Don't leave this session's locals around for the next one to read
            IsExecuting = false;
            IsDebugging = false;
            IsPaused = false;
            CurrentPausedLine = -1;
            RequestSetPausedLine?.Invoke(-1);
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
        ScriptDebugSession.Current?.Stop();
        _executionCts?.Cancel();
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
