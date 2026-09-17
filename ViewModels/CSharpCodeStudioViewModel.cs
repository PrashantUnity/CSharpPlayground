using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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

    // Code Studio reuses one long-lived ViewModel instance across every script you open (see
    // CSharpStudioHostViewModel.NavigateToCodeStudio -> UpdateActiveScriptAsync), so switching scripts from
    // the Explorer sidebar without leaving the page never actually changes the View's DataContext
    // reference — nothing re-fires OnDataContextChanged, which is the only place the editor's text is
    // normally pushed in. Without an explicit signal here, the editor would keep showing the PREVIOUS
    // script's code while the title/breadcrumb/everything else correctly shows the new one, and a
    // subsequent Save would overwrite the new script's file with the old script's content.
    public event Action? RequestReloadEditorText;

    public ObservableCollection<ExplorerItemViewModel> ExplorerRootItems { get; } = new();

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
        PopulateExplorerTree();
    }

    // Must be awaited, never called fire-and-forget-then-blocked-on: the Explorer refresh at the end
    // touches storage that now does genuine async file I/O (see LocalScriptStorageService's project-file
    // reads), and this is called directly from UI-thread event handlers (NavigateToCodeStudio, Explorer
    // clicks). A synchronous PopulateExplorerTree().GetAwaiter().GetResult() here previously deadlocked
    // the UI thread the moment that I/O actually needed to yield — that's the "opening a script hangs
    // the app" bug. PopulateExplorerTree()'s own blocking wait stays safe only because the host
    // constructs both studio ViewModels inside Task.Run, off the UI thread.
    public async Task UpdateActiveScriptAsync(ScriptDocumentItem script)
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
        RequestReloadEditorText?.Invoke();

        TriggerDiagnosticsCheck();
        await RefreshExplorerAsync();
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

    // ================================================================
    // EXPLORER (script/folder tree) — mirrors CSharpNotebookStudioViewModel's Explorer, adapted for
    // Code Studio's single-open-document model (no tab strip): "opening" a different script switches
    // this same ViewModel's Script/Code in place via UpdateActiveScriptAsync rather than adding a tab.
    // ================================================================

    public void PopulateExplorerTree()
    {
        try
        {
            var folderPaths = _storageService.LoadFolderPathsAsync().GetAwaiter().GetResult();
            var summaries = _storageService.LoadWorkspaceSummariesAsync().GetAwaiter().GetResult();
            RebuildExplorerTree(folderPaths, summaries);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to populate script explorer tree: {ex.Message}");
            ExplorerRootItems.Clear();
        }
    }


    [RelayCommand]
    public async Task RefreshExplorerAsync()
    {
        try
        {
            var folderPaths = await _storageService.LoadFolderPathsAsync();
            var summaries = await _storageService.LoadWorkspaceSummariesAsync();
            RebuildExplorerTree(folderPaths, summaries);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to refresh script explorer tree: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the visible tree purely from real storage: real folders (LoadFolderPathsAsync) plus real
    /// scripts (LoadWorkspaceSummariesAsync, placed under their actual FolderPath). Same shape as
    /// Notebook Studio's tree, including the external-project relevance filter — an externally-saved
    /// script's project only shows up while the currently open Script belongs to it, rather than
    /// merging every external folder you've ever saved a script to into one workspace. Code Studio has
    /// just one open document at a time, so that's at most a single relevant external folder (versus
    /// Notebook Studio's set-of-open-tabs version of the same filter).
    /// </summary>
    private void RebuildExplorerTree(List<string> folderPaths, List<WorkspaceItemSummary> summaries)
    {
        ExplorerRootItems.Clear();
        var folderNodes = new Dictionary<string, ExplorerItemViewModel>(StringComparer.OrdinalIgnoreCase);

        ExplorerItemViewModel? GetOrCreateFolder(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (folderNodes.TryGetValue(relativePath, out var existing)) return existing;

            var lastSlash = relativePath.LastIndexOf('/');
            var name = lastSlash >= 0 ? relativePath[(lastSlash + 1)..] : relativePath;
            var parentPath = lastSlash >= 0 ? relativePath[..lastSlash] : string.Empty;
            var parent = GetOrCreateFolder(parentPath);

            var node = CreateFolderItem(name, relativePath, isExpanded: false, parent: parent);
            AddToTree(parent, node);
            folderNodes[relativePath] = node;
            return node;
        }

        foreach (var path in folderPaths.OrderBy(p => p.Count(c => c == '/')).ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            GetOrCreateFolder(path);
        }

        var externalGroupNodes = new Dictionary<string, ExplorerItemViewModel>(StringComparer.OrdinalIgnoreCase);

        ExplorerItemViewModel GetOrCreateExternalGroup(string absolutePath)
        {
            if (externalGroupNodes.TryGetValue(absolutePath, out var existing)) return existing;

            var trimmed = absolutePath.TrimEnd('/', '\\');
            var name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name)) name = trimmed;

            var node = CreateFolderItem(name, absolutePath, isExpanded: false, parent: null, isExternalGroup: true);
            AddToTree(null, node);
            externalGroupNodes[absolutePath] = node;
            return node;
        }

        string? relevantExternalFolder = null;
        if (Script != null)
        {
            var activeSummary = summaries.FirstOrDefault(s => string.Equals(s.Id, Script.Id, StringComparison.OrdinalIgnoreCase));
            if (activeSummary != null && !string.IsNullOrEmpty(activeSummary.FolderPath) && Path.IsPathRooted(activeSummary.FolderPath))
            {
                relevantExternalFolder = activeSummary.FolderPath;
            }
        }

        foreach (var s in summaries.Where(x => x.IsScript).OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
        {
            var name = s.Title.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase) ? s.Title : $"{s.Title}.frycs";
            var isExternal = !string.IsNullOrEmpty(s.FolderPath) && Path.IsPathRooted(s.FolderPath);
            if (isExternal && !string.Equals(s.FolderPath, relevantExternalFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parent = isExternal ? GetOrCreateExternalGroup(s.FolderPath!) : GetOrCreateFolder(s.FolderPath);
            var fullPath = isExternal
                ? $"{s.FolderPath!.TrimEnd('/', '\\')}/{name}"
                : (string.IsNullOrEmpty(s.FolderPath) ? name : $"{s.FolderPath}/{name}");

            var siblings = parent?.Children ?? (IEnumerable<ExplorerItemViewModel>)ExplorerRootItems;
            if (siblings.Any(c => !c.IsDirectory && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var docItem = CreateFileItem(name, s.Id, parent, fullPath);
            AddToTree(parent, docItem);
        }

        // Ensure the currently open script is represented even if it hasn't reached storage yet.
        if (Script != null && !string.IsNullOrEmpty(Script.Id) && FindByDocumentId(ExplorerRootItems, Script.Id) == null)
        {
            var fileName = Script.Title.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase) ? Script.Title : $"{Script.Title}.frycs";
            var expItem = CreateFileItem(fileName, Script.Id, parent: null, fullPath: fileName);
            ExplorerRootItems.Add(expItem);
        }

        SortExplorerTree(ExplorerRootItems);
        HighlightExplorerItem(Script?.Id);
    }

    private void SortExplorerTree(ObservableCollection<ExplorerItemViewModel> items)
    {
        var sorted = items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (!sorted.SequenceEqual(items))
        {
            items.Clear();
            foreach (var item in sorted) items.Add(item);
        }

        foreach (var folder in items.Where(i => i.IsDirectory))
        {
            SortExplorerTree(folder.Children);
        }
    }

    private void DeselectAll(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var it in items)
        {
            it.IsSelected = false;
            if (it.Children.Count > 0) DeselectAll(it.Children);
        }
    }

    private void HighlightExplorerItem(string? documentId)
    {
        DeselectAll(ExplorerRootItems);
        if (string.IsNullOrEmpty(documentId)) return;

        var match = FindByDocumentId(ExplorerRootItems, documentId);
        if (match != null)
        {
            match.IsSelected = true;
            var parent = match.Parent;
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent = parent.Parent;
            }
        }
    }

    private ExplorerItemViewModel? FindByDocumentId(IEnumerable<ExplorerItemViewModel> items, string documentId)
    {
        foreach (var item in items)
        {
            if (!item.IsDirectory && string.Equals(item.DocumentId, documentId, StringComparison.OrdinalIgnoreCase)) return item;
            var childMatch = FindByDocumentId(item.Children, documentId);
            if (childMatch != null) return childMatch;
        }
        return null;
    }

    private ExplorerItemViewModel? FindSelectedItem(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var item in items)
        {
            if (item.IsSelected) return item;
            var childMatch = FindSelectedItem(item.Children);
            if (childMatch != null) return childMatch;
        }
        return null;
    }

    private void AddToTree(ExplorerItemViewModel? parent, ExplorerItemViewModel child)
    {
        if (parent != null) parent.Children.Add(child);
        else ExplorerRootItems.Add(child);
    }

    private ExplorerItemViewModel CreateFolderItem(string name, string fullPath, bool isExpanded = false, ExplorerItemViewModel? parent = null, bool isExternalGroup = false)
    {
        return new ExplorerItemViewModel
        {
            Name = name,
            IsDirectory = true,
            IsExternalGroup = isExternalGroup,
            IsExpanded = isExpanded,
            Parent = parent,
            Depth = (parent?.Depth ?? -1) + 1,
            FullPath = fullPath,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewScriptUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
    }

    private ExplorerItemViewModel CreateFileItem(string name, string? documentId, ExplorerItemViewModel? parent, string fullPath)
    {
        return new ExplorerItemViewModel
        {
            Name = name,
            DocumentId = documentId,
            IsDirectory = false,
            FileExtension = Path.GetExtension(name),
            Parent = parent,
            Depth = (parent?.Depth ?? -1) + 1,
            FullPath = fullPath,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewScriptUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
    }

    private void OnExplorerItemClicked(ExplorerItemViewModel item) => _ = SwitchToScriptAsync(item);

    /// <summary>
    /// The Code Studio equivalent of "open this document": since there's only ever one script open at
    /// a time (no tab strip), clicking a different script in the Explorer auto-saves the current one
    /// first — matching the existing silent-save-before-navigating-away behavior already used by Back
    /// to Hub/Home — then switches this same ViewModel onto the clicked script in place.
    /// </summary>
    public async Task SwitchToScriptAsync(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
            return;
        }

        if (string.IsNullOrEmpty(item.DocumentId)) return;
        if (Script != null && string.Equals(Script.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase))
        {
            HighlightExplorerItem(item.DocumentId);
            return;
        }

        await SaveAsync();

        var loaded = await _storageService.LoadScriptAsync(item.DocumentId);
        if (loaded == null) return;

        await UpdateActiveScriptAsync(loaded);
    }

    public void DeleteExplorerItem(ExplorerItemViewModel item) => _ = DeleteExplorerItemAsync(item);

    [RelayCommand]
    public async Task DeleteExplorerItemAsync(ExplorerItemViewModel item)
    {
        if (item == null || item.IsExternalGroup) return;

        if (item.IsDirectory)
        {
            try
            {
                await _storageService.DeleteFolderAsync(item.FullPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to delete folder '{item.FullPath}': {ex.Message}");
            }
        }
        else if (!string.IsNullOrEmpty(item.DocumentId))
        {
            try
            {
                await _storageService.DeleteItemAsync(item.DocumentId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to delete '{item.DocumentId}': {ex.Message}");
            }
        }

        if (item.Parent != null)
        {
            var parent = item.Parent;
            parent.Children.Remove(item);
            if (parent.IsExternalGroup && parent.Children.Count == 0)
            {
                ExplorerRootItems.Remove(parent);
            }
        }
        else
        {
            ExplorerRootItems.Remove(item);
        }
    }

    // Header-toolbar entry points (no specific tree item to hang off of) — resolve a target folder
    // from whatever's currently selected, falling back to a plain root-level create when nothing is
    // selected, exactly like Notebook Studio's NewFile/NewFolder. NewFileUnderItemAsync/
    // NewFolderUnderItemAsync always assume a non-null target, so this guard is what keeps the
    // header buttons from passing one through as null.
    [RelayCommand]
    public async Task NewScript()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        var targetFolder = (selected != null && selected.IsDirectory) ? selected : selected?.Parent;

        if (targetFolder != null)
        {
            await NewScriptUnderItemAsync(targetFolder);
            return;
        }

        var timestamp = DateTime.Now.ToString("HHmmss");
        var title = $"Script_{timestamp}";
        ScriptDocumentItem newDoc;
        try
        {
            newDoc = await _storageService.CreateNewScriptAsync(title);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create script: {ex.Message}");
            return;
        }

        await SaveAsync();
        await UpdateActiveScriptAsync(newDoc);
        FindByDocumentId(ExplorerRootItems, newDoc.Id)?.StartRename();
    }

    [RelayCommand]
    public async Task NewFolder()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        var targetFolder = (selected != null && selected.IsDirectory) ? selected : selected?.Parent;
        await CreateFolderCoreAsync(targetFolder);
    }

    public void NewScriptUnderItem(ExplorerItemViewModel target) => _ = NewScriptUnderItemAsync(target);

    [RelayCommand]
    public async Task NewScriptUnderItemAsync(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : target.Parent;
        var timestamp = DateTime.Now.ToString("HHmmss");
        var title = $"Script_{timestamp}";
        var folderPath = folder?.FullPath;

        ScriptDocumentItem newDoc;
        try
        {
            newDoc = await _storageService.CreateNewScriptAsync(title, folderPath: folderPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create script: {ex.Message}");
            return;
        }

        await SaveAsync();
        await UpdateActiveScriptAsync(newDoc);

        var newItem = FindByDocumentId(ExplorerRootItems, newDoc.Id);
        newItem?.StartRename();
    }

    public void NewFolderUnderItem(ExplorerItemViewModel target) => _ = NewFolderUnderItemAsync(target);

    [RelayCommand]
    public async Task NewFolderUnderItemAsync(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : target.Parent;
        await CreateFolderCoreAsync(folder);
    }

    private async Task CreateFolderCoreAsync(ExplorerItemViewModel? parentFolder)
    {
        string newRelativePath;
        try
        {
            newRelativePath = await _storageService.CreateFolderAsync(parentFolder?.FullPath, "New Folder");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create folder: {ex.Message}");
            return;
        }

        var name = newRelativePath.Contains('/') ? newRelativePath[(newRelativePath.LastIndexOf('/') + 1)..] : newRelativePath;
        var newFolder = CreateFolderItem(name, newRelativePath, isExpanded: true, parent: parentFolder);
        AddToTree(parentFolder, newFolder);
        if (parentFolder != null) parentFolder.IsExpanded = true;
        newFolder.StartRename();
    }

    public void DuplicateExplorerItem(ExplorerItemViewModel item) => _ = DuplicateExplorerItemAsync(item);

    [RelayCommand]
    public async Task DuplicateExplorerItemAsync(ExplorerItemViewModel item)
    {
        if (item == null || item.IsDirectory || string.IsNullOrEmpty(item.DocumentId)) return;

        var parent = item.Parent;
        var originalTitle = item.Name.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase)
            ? item.Name.Substring(0, item.Name.Length - 6)
            : item.Name;
        var copyTitle = $"{originalTitle} Copy";
        var copyFileName = $"{copyTitle}.frycs";

        // Duplicating the currently open (possibly unsaved) script copies its live in-memory state;
        // any other script is duplicated from whatever's already on disk.
        var isActive = Script != null && string.Equals(Script.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase);
        var origDoc = isActive ? Script : await _storageService.LoadScriptAsync(item.DocumentId);

        var copyDoc = new ScriptDocumentItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = copyTitle,
            Category = origDoc?.Category ?? "Custom",
            Description = origDoc?.Description ?? "",
            ExecutionMode = origDoc?.ExecutionMode ?? "Statements",
            Code = origDoc?.Code ?? string.Empty,
            Notes = origDoc?.Notes ?? string.Empty,
            TestCases = origDoc?.TestCases?
                .Select(tc => new TestCaseItem { Name = tc.Name, Input = tc.Input, ExpectedOutput = tc.ExpectedOutput })
                .ToList() ?? new List<TestCaseItem>(),
            Created = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        // copyDoc has never been saved before, so SaveScriptAsync's folderPath is what decides where
        // it's actually written — without passing the original's external folder through here,
        // duplicating a script that lives outside the library would silently move the copy back into
        // the library root instead of keeping it next to the original.
        var externalFolderPath = parent?.IsExternalGroup == true ? parent.FullPath : null;
        await _storageService.SaveScriptAsync(copyDoc, externalFolderPath);

        var copyPath = string.IsNullOrEmpty(parent?.FullPath) ? copyFileName : $"{parent!.FullPath}/{copyFileName}";
        var copyItem = CreateFileItem(copyFileName, copyDoc.Id, parent, copyPath);
        AddToTree(parent, copyItem);
        if (parent != null) parent.IsExpanded = true;

        await SwitchToScriptAsync(copyItem);
    }

    private void OnItemRenamed(ExplorerItemViewModel item) => _ = OnItemRenamedAsync(item);

    internal async Task OnItemRenamedAsync(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            // This node's FullPath is a real external directory when IsExternalGroup — RenameFolderAsync
            // would otherwise act on it directly (see the identical guard in Notebook Studio).
            if (item.IsExternalGroup) return;

            try
            {
                var oldPath = item.FullPath;
                var newPath = await _storageService.RenameFolderAsync(oldPath, item.Name);
                UpdateDescendantFullPaths(item, oldPath, newPath);
                item.FullPath = newPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to rename folder '{item.FullPath}': {ex.Message}");
            }
            return;
        }

        if (string.IsNullOrEmpty(item.DocumentId)) return;

        var newTitle = item.Name.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase)
            ? item.Name.Substring(0, item.Name.Length - 6)
            : item.Name;

        if (Script != null && string.Equals(Script.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase))
        {
            Script.Title = newTitle;
            OnPropertyChanged(nameof(Script));
            await SaveAsync();
        }
        else
        {
            var doc = await _storageService.LoadScriptAsync(item.DocumentId);
            if (doc != null)
            {
                doc.Title = newTitle;
                await _storageService.SaveScriptAsync(doc);
            }
        }
    }

    private void UpdateDescendantFullPaths(ExplorerItemViewModel node, string oldPrefix, string newPrefix)
    {
        foreach (var child in node.Children)
        {
            if (child.FullPath.StartsWith(oldPrefix, StringComparison.Ordinal))
            {
                child.FullPath = newPrefix + child.FullPath[oldPrefix.Length..];
            }
            UpdateDescendantFullPaths(child, oldPrefix, newPrefix);
        }
    }

    [RelayCommand]
    public void CopyItemPath(ExplorerItemViewModel item)
    {
        if (item == null) return;
        var path = !string.IsNullOrEmpty(item.FullPath) ? item.FullPath : item.Name;
        CompilerStatusText = $"Path: {path}";
    }

    [RelayCommand]
    public void CollapseAllExplorer()
    {
        foreach (var item in ExplorerRootItems)
        {
            CollapseItemRecursive(item);
        }
    }

    private void CollapseItemRecursive(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.IsExpanded = false;
            foreach (var child in item.Children)
            {
                CollapseItemRecursive(child);
            }
        }
    }
}
