using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpCodeStudioViewModel
{
    [ObservableProperty]
    private int _selectedBottomTabIndex = 0;

    [ObservableProperty]
    private bool _isBottomDeckExpanded = true;

    [ObservableProperty]
    private Avalonia.Controls.GridLength _bottomDeckGridLength = new(280, Avalonia.Controls.GridUnitType.Pixel);

    private double _savedBottomDeckHeight = 280;

    partial void OnIsBottomDeckExpandedChanged(bool value)
    {
        if (value)
        {
            BottomDeckGridLength = new Avalonia.Controls.GridLength(_savedBottomDeckHeight > 60 ? _savedBottomDeckHeight : 280, Avalonia.Controls.GridUnitType.Pixel);
        }
        else
        {
            if (BottomDeckGridLength.IsAbsolute && BottomDeckGridLength.Value > 60)
            {
                _savedBottomDeckHeight = BottomDeckGridLength.Value;
            }
            BottomDeckGridLength = new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Pixel);
        }
    }

    [ObservableProperty]
    private bool _isExecuting;

    partial void OnIsExecutingChanged(bool value)
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

    public ObservableCollection<DumpTableResult> DumpResults { get; } = new();
    public ObservableCollection<RichCellOutput> RichOutputs { get; } = new();

    /// <summary>
    /// True when the Results tab has nothing to draw, so it shows its "No Visual Dumps Yet" hint: no table dumps and no
    /// image, HTML, chart, visualizer, control or object-inspector output. Text and error outputs aren't drawn there.
    /// </summary>
    public bool HasNoResults => DumpResults.Count == 0 && !RichOutputs.Any(IsDrawnInResults);

    // The kinds the Results tab's RichOutputs template draws (StudioBottomDeckControl.axaml).
    private static bool IsDrawnInResults(RichCellOutput output) =>
        output.IsImageKind || output.IsHtmlKind || output.IsControlKind || output.IsInspectorKind;
    public ObservableCollection<DiagnosticItemViewModel> Diagnostics { get; } = new();
    public ObservableCollection<AssemblyReferenceViewModel> References { get; } = new();
    public ObservableCollection<TestCaseItem> TestCases { get; } = new();

    private void TriggerDiagnosticsCheck()
    {
        _diagnosticsCts?.Cancel();
        _diagnosticsCts = new CancellationTokenSource();
        var token = _diagnosticsCts.Token;

        var targetScriptId = Script.Id;
        var codeSnapshot = Code;
        var mode = CurrentLanguageMode;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;

                if (Script.Id == targetScriptId)
                {
                    CompilerStatusText = "Analyzing...";
                }
                var items = _compilerService.CheckDiagnostics(codeSnapshot, mode);

                if (token.IsCancellationRequested) return;

                Action applyDiagnostics = () =>
                {
                    var targetTab = OpenTabs.FirstOrDefault(t => t.Id == targetScriptId);
                    if (targetTab != null)
                    {
                        targetTab.Diagnostics.Clear();
                        foreach (var item in items)
                        {
                            targetTab.Diagnostics.Add(new DiagnosticItemViewModel(item, (l, c) =>
                            {
                                RequestNavigateToCaret?.Invoke(l, c);
                            }));
                        }
                    }

                    if (Script.Id == targetScriptId)
                    {
                        Diagnostics.Clear();
                        foreach (var item in items)
                        {
                            Diagnostics.Add(new DiagnosticItemViewModel(item, (l, c) =>
                            {
                                SetCaretPosition(l, c);
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
                    }
                };

                if (Avalonia.Application.Current != null && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(applyDiagnostics);
                }
                else
                {
                    applyDiagnostics();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

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

        var runningTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (runningTab != null)
        {
            runningTab.IsExecuting = true;
            runningTab.ConsoleOutput = "🚀 Running C# code (.Dump enabled)...\n";
            runningTab.DumpResults.Clear();
            runningTab.RichOutputs.Clear();
        }

        DisposeRichOutputControls();
        DumpResults.Clear();
        RichOutputs.Clear();
        SelectedBottomTabIndex = 0;
        IsBottomDeckExpanded = true;
        ConsoleOutput = "🚀 Running C# code (.Dump enabled)...\n";
        CompilerStatusText = "Executing...";
        IsExecuting = true;

        _executionCts?.Cancel();
        _executionCts = new CancellationTokenSource();
        if (runningTab != null) runningTab.ExecutionCts = _executionCts;

        // 0 = no automatic timeout (the default) — execution runs until Stop is clicked, matching
        // Jupyter. A positive value is an opt-in ceiling for whoever configures one.
        var timeoutSeconds = _getTimeoutSeconds();
        using var timeoutCts = timeoutSeconds > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds))
            : new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_executionCts.Token, timeoutCts.Token);
        var token = linkedCts.Token;

        // Identifies this specific run so callbacks/continuations from an execution we later give up
        // on (see ExecutionAbandonment usage below) can never write stale output/status after a
        // newer run has already started.
        var myRunId = ++_executionRunId;

        // Everything the script prints reaches the Terminal once and in order, however its queued live writes and the
        // end of the run interleave (see RunConsoleRelay).
        var terminal = new RunConsoleRelay(text =>
        {
            if (myRunId != _executionRunId) return;
            if (runningTab != null)
            {
                runningTab.ConsoleOutput += text;
                if (runningTab.IsActive)
                {
                    ConsoleOutput = runningTab.ConsoleOutput;
                }
            }
            else
            {
                ConsoleOutput += text;
            }
        }, _postToUiThread);

        using var scope = InteractiveDisplayContext.EnterScope(richOutput =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (runningTab != null)
                {
                    runningTab.RichOutputs.Add(richOutput);
                    if (richOutput.TableResult != null)
                    {
                        runningTab.DumpResults.Add(richOutput.TableResult);
                    }
                }
                if (runningTab == null || runningTab.IsActive)
                {
                    RichOutputs.Add(richOutput);
                    if (richOutput.TableResult != null)
                    {
                        DumpResults.Add(richOutput.TableResult);
                        SelectedBottomTabIndex = 0;
                    }
                }
            });
        });
        using var cancellationScope = InteractiveCancellationContext.EnterScope(token);

        try
        {
            if (CurrentLanguageMode == ExecutionLanguageMode.Statements || CurrentLanguageMode == ExecutionLanguageMode.Expression)
            {
                _kernel.HardReset();

                var codeToRun = Code;
                if (CurrentLanguageMode == ExecutionLanguageMode.Expression)
                {
                    var expr = Code.Trim().TrimEnd(';');
                    codeToRun = $"({expr}).Dump();";
                }

                // Roslyn script execution must never run inline on the UI thread: if the user's code
                // does something like `httpClient.GetStringAsync(url).Result`, its continuation
                // needs to resume on the same UI thread that's blocked waiting for it — an
                // unrecoverable deadlock that freezes the whole app (and makes Stop unclickable).
                // Task.Run keeps it on a thread-pool thread with no captured SynchronizationContext,
                // matching the same pattern ScriptExecutionEngine.ExecuteAsync already uses safely.
                var kernelExecutionTask = Task.Run(() => _kernel.ExecuteCellAsync(
                    codeToRun,
                    ct: token,
                    onLiveConsole: terminal.Write,
                    onRichOutput: rich =>
                    {
                        Action appendRich = () =>
                        {
                            if (myRunId != _executionRunId) return;
                            if (runningTab != null)
                            {
                                runningTab.RichOutputs.Add(rich);
                                if (rich.TableResult != null)
                                {
                                    runningTab.DumpResults.Add(rich.TableResult);
                                }
                            }
                            if (runningTab == null || runningTab.IsActive)
                            {
                                RichOutputs.Add(rich);
                                if (rich.TableResult != null)
                                {
                                    DumpResults.Add(rich.TableResult);
                                    SelectedBottomTabIndex = 0;
                                }
                            }
                        };

                        if (Avalonia.Application.Current != null && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(appendRich);
                        }
                        else
                        {
                            appendRich();
                        }
                    }));

                KernelExecutionResult kernelResult;
                if (await ExecutionAbandonment.WaitWithGraceAsync(kernelExecutionTask, token))
                {
                    kernelResult = await kernelExecutionTask;
                }
                else
                {
                    // Stop/timeout fired and execution didn't respond within the grace period —
                    // almost always because it's synchronously blocked mid-statement (e.g. a hung
                    // HttpClient call), which Roslyn's cancellation can never preempt. Give up
                    // waiting rather than hang this command forever; the thread-pool thread keeps
                    // running in the background until it naturally returns or the process exits.
                    kernelResult = new KernelExecutionResult
                    {
                        WasCancelled = true,
                        ErrorMessage = "Execution did not respond to Stop and was abandoned."
                    };

                    var abandonedRunId = myRunId;
                    _ = kernelExecutionTask.ContinueWith(t =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (abandonedRunId != _executionRunId) return; // a newer run already took over
                            var statusText = t.IsFaulted
                                ? "Background task (previously abandoned) finished with an error"
                                : "Background task (previously abandoned) finished";
                            if (runningTab != null) runningTab.CompilerStatusText = statusText;
                            if (runningTab == null || runningTab.IsActive) CompilerStatusText = statusText;
                        });
                    }, TaskScheduler.Default);
                }

                // The kernel's ConsoleOutput repeats what it wrote live; it only adds text that never arrived live.
                terminal.Complete(kernelResult.Success ? kernelResult.ConsoleOutput : null);

                if (kernelResult.Success)
                {
                    var timeText = $"{kernelResult.Elapsed.TotalMilliseconds:N0} ms";
                    var statusText = DumpResults.Count > 0
                        ? $"Completed • {DumpResults.Count} visual dump{(DumpResults.Count == 1 ? "" : "s")}"
                        : "Completed";
                    Script.ExecutionCount++;
                    _ = _storageService.SaveScriptAsync(Script);

                    if (runningTab != null)
                    {
                        runningTab.ExecutionTimeText = timeText;
                        runningTab.CompilerStatusText = statusText;
                    }
                    if (runningTab == null || runningTab.IsActive)
                    {
                        if (runningTab != null)
                        {
                            ConsoleOutput = runningTab.ConsoleOutput;
                        }
                        ExecutionTimeText = timeText;
                        CompilerStatusText = statusText;
                        SelectedBottomTabIndex = DumpResults.Count > 0 ? 0 : 1;
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
                        SelectedBottomTabIndex = 2;
                    }
                    else
                    {
                        SelectedBottomTabIndex = 1;
                    }
                }
            }
            else
            {
                var (success, bytes, diagnostics) = await Task.Run(() =>
                    _compilerService.CompileToAssembly(Code, CurrentLanguageMode));

                if (!success || bytes == null)
                {
                    var failMsg = "❌ Compilation failed. Check the Problems tab for details.\n";
                    foreach (var diag in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        failMsg += $"  • {diag.LocationString}: {diag.Id} {diag.Message}\n";
                    }
                    if (runningTab != null) runningTab.ConsoleOutput += failMsg;
                    if (runningTab == null || runningTab.IsActive)
                    {
                        ConsoleOutput += failMsg;
                        CompilerStatusText = "Build Failed";
                        SelectedBottomTabIndex = 2;
                    }
                    return;
                }

                var startMsg = "[Build] Succeeded. Executing in-memory...\n--------------------------------------------------\n";
                if (runningTab != null) runningTab.ConsoleOutput += startMsg;
                if (runningTab == null || runningTab.IsActive)
                {
                    ConsoleOutput += startMsg;
                    CompilerStatusText = "Running...";
                }

                var executionTask = _executionEngine.ExecuteAsync(bytes, terminal.Write, token);

                ExecutionResult result;
                if (await ExecutionAbandonment.WaitWithGraceAsync(executionTask, token))
                {
                    result = await executionTask;
                }
                else
                {
                    // See the Statements/Expression branch above: cancellation only stops a
                    // compiled Main() *before* it starts (ScriptExecutionEngine.ExecuteAsync checks
                    // the token once, then invokes the entry point) — a synchronously-blocked call
                    // inside it can't be preempted either. Give up waiting rather than hang forever.
                    result = new ExecutionResult { WasCancelled = true };

                    var abandonedRunId = myRunId;
                    _ = executionTask.ContinueWith(t =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (abandonedRunId != _executionRunId) return; // a newer run already took over
                            var statusText = t.IsFaulted
                                ? "Background task (previously abandoned) finished with an error"
                                : "Background task (previously abandoned) finished";
                            if (runningTab != null) runningTab.CompilerStatusText = statusText;
                            if (runningTab == null || runningTab.IsActive) CompilerStatusText = statusText;
                        });
                    }, TaskScheduler.Default);
                }

                // Before the closing lines, so they always come after the program's own output.
                terminal.Complete();

                var endMsg = "\n--------------------------------------------------\n";
                if (result.Success)
                {
                    endMsg += $"✅ Execution finished in {result.Elapsed.TotalMilliseconds:N0} ms\n";
                    var timeText = $"{result.Elapsed.TotalMilliseconds:N0} ms";
                    var statusText = DumpResults.Count > 0
                        ? $"Completed • {DumpResults.Count} visual dump{(DumpResults.Count == 1 ? "" : "s")}"
                        : "Completed";
                    Script.ExecutionCount++;
                    _ = _storageService.SaveScriptAsync(Script);

                    if (runningTab != null)
                    {
                        runningTab.ConsoleOutput += endMsg;
                        runningTab.ExecutionTimeText = timeText;
                        runningTab.CompilerStatusText = statusText;
                    }
                    if (runningTab == null || runningTab.IsActive)
                    {
                        ConsoleOutput += endMsg;
                        ExecutionTimeText = timeText;
                        CompilerStatusText = statusText;
                        SelectedBottomTabIndex = DumpResults.Count > 0 ? 0 : 1;
                    }
                }
                else if (result.WasCancelled)
                {
                    var cancelMsg = timeoutCts.IsCancellationRequested
                        ? $"⏱️ Execution timed out after {timeoutSeconds}s.\n"
                        : "⚠️ Execution was cancelled.\n";
                    var statusText = timeoutCts.IsCancellationRequested
                        ? $"⏱️ Timed out after {timeoutSeconds}s"
                        : "🛑 Cancelled";

                    if (runningTab != null)
                    {
                        runningTab.ConsoleOutput += cancelMsg;
                        runningTab.CompilerStatusText = statusText;
                    }
                    if (runningTab == null || runningTab.IsActive)
                    {
                        ConsoleOutput += cancelMsg;
                        CompilerStatusText = statusText;
                    }
                }
                else
                {
                    var errText = $"❌ Execution failed: {result.Error}\n";
                    if (runningTab != null)
                    {
                        runningTab.ConsoleOutput += errText;
                        runningTab.CompilerStatusText = "Runtime Error";
                    }
                    if (runningTab == null || runningTab.IsActive)
                    {
                        ConsoleOutput += errText;
                        CompilerStatusText = "Runtime Error";
                    }
                }
            }
        }
        finally
        {
            if (runningTab != null)
            {
                runningTab.IsExecuting = false;
                runningTab.ExecutionTimeText = ExecutionTimeText;
                runningTab.CompilerStatusText = CompilerStatusText;
            }
            if (runningTab == null || runningTab.IsActive)
            {
                IsExecuting = false;
            }
        }
    }

    // How a running script's output reaches the UI thread unless the constructor was given another way: queued when it
    // comes from another thread, right away on the UI thread or with no app at all (unit tests).
    private static void RunOnUiThread(Action action)
    {
        if (Avalonia.Application.Current != null && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
        else
        {
            action();
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
            await RunCodeAsync();

            // Scripts that check themselves with Judge print a ✅/❌ line per case; that line is the verdict.
            // Anything else falls back to "the output mentions the expected text".
            var verdict = Judge.Verdict(ConsoleOutput, testCase.Name);
            testCase.ActualOutput = Judge.LineFor(ConsoleOutput, testCase.Name) ?? ConsoleOutput;
            bool passed = verdict ?? (!string.IsNullOrEmpty(testCase.ExpectedOutput) && ConsoleOutput.Contains(testCase.ExpectedOutput));
            if (passed)
            {
                testCase.Passed = true;
                CheckAndMarkBlindProblemSolved();
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

    private void CheckAndMarkBlindProblemSolved()
    {
        try
        {
            if (TestCases.Count > 0 && TestCases.All(tc => tc.Passed == true))
            {
                var match = System.Text.RegularExpressions.Regex.Match(Script.Title, @"^(\d+)\.\s");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int problemNum))
                {
                    _ = new LocalBlindProgressService().SetProblemSolvedAsync(problemNum, true);
                }
            }
        }
        catch
        {
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
        var targetTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        targetTab?.ExecutionCts?.Cancel();
        _executionCts?.Cancel();
        if (targetTab != null)
        {
            targetTab.ConsoleOutput += "\n🛑 Cancellation requested by user...\n";
            targetTab.CompilerStatusText = "Stopping...";
        }
        ConsoleOutput += "\n🛑 Cancellation requested by user...\n";
        CompilerStatusText = "Stopping...";
    }

    [RelayCommand]
    private void ClearConsole()
    {
        ConsoleOutput = string.Empty;
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
    public void ToggleBottomDeck()
    {
        IsBottomDeckExpanded = !IsBottomDeckExpanded;
    }

    [RelayCommand]
    public void ShowProblemsTab()
    {
        SelectedBottomTabIndex = 2;
        IsBottomDeckExpanded = true;
    }

    [RelayCommand]
    public void ShowConsoleTab()
    {
        SelectedBottomTabIndex = 1;
        IsBottomDeckExpanded = true;
    }

    [RelayCommand]
    public void ShowDumpResultsTab()
    {
        SelectedBottomTabIndex = 0;
        IsBottomDeckExpanded = true;
    }

    [RelayCommand]
    public void ShowTestCasesTab()
    {
        SelectedBottomTabIndex = 3;
        IsBottomDeckExpanded = true;
    }

    [RelayCommand]
    public void ShowDebuggerTab()
    {
        SelectedBottomTabIndex = 4;
        IsBottomDeckExpanded = true;
    }
}
