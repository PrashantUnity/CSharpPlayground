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

    // ── VS Code Multi-Tab Document Strip ──
    public ObservableCollection<StudioTabItemViewModel> OpenTabs { get; } = new();

    // ── VS Code Layout: Activity Bar & Primary Side Bar ──
    // 0=Explorer, 1=Search, 2=Debug, 3=NuGet, 4=Scratchpad, 5=Problems
    [ObservableProperty]
    private int _selectedActivityBarIndex = 0;

    [ObservableProperty]
    private bool _isSideBarVisible = true;

    [ObservableProperty]
    private Avalonia.Controls.GridLength _sideBarGridLength = new(280, Avalonia.Controls.GridUnitType.Pixel);

    private double _savedSideBarWidth = 280;

    partial void OnIsSideBarVisibleChanged(bool value)
    {
        if (value)
        {
            SideBarGridLength = new Avalonia.Controls.GridLength(_savedSideBarWidth > 120 ? _savedSideBarWidth : 280, Avalonia.Controls.GridUnitType.Pixel);
        }
        else
        {
            if (SideBarGridLength.IsAbsolute && SideBarGridLength.Value > 120)
            {
                _savedSideBarWidth = SideBarGridLength.Value;
            }
            SideBarGridLength = new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Pixel);
        }
    }

    [ObservableProperty]
    private string _sideBarTitle = "EXPLORER";

    [ObservableProperty]
    private int _selectedLeftTabIndex = -1;

    public bool IsExplorerActive => SelectedActivityBarIndex == 0;
    public bool IsSearchActive => SelectedActivityBarIndex == 1;
    public bool IsDebugActive => SelectedActivityBarIndex == 2;
    public bool IsDependenciesActive => SelectedActivityBarIndex == 3;
    public bool IsScratchpadActive => SelectedActivityBarIndex == 4;
    public bool IsProblemsActive => SelectedActivityBarIndex == 5;

    partial void OnSelectedActivityBarIndexChanged(int value)
    {
        SideBarTitle = value switch
        {
            1 => "SEARCH",
            2 => "RUN AND DEBUG",
            3 => "DEPENDENCIES & NUGET",
            4 => "SCRATCHPAD & NOTES",
            5 => "PROBLEMS",
            _ => "EXPLORER"
        };

        OnPropertyChanged(nameof(IsExplorerActive));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(IsDebugActive));
        OnPropertyChanged(nameof(IsDependenciesActive));
        OnPropertyChanged(nameof(IsScratchpadActive));
        OnPropertyChanged(nameof(IsProblemsActive));
    }

    partial void OnSelectedLeftTabIndexChanged(int value)
    {
        switch (value)
        {
            case 0:
                SelectedActivityBarIndex = 4; // Scratchpad & Notes
                IsSideBarVisible = true;
                break;
            case 1:
                SelectedActivityBarIndex = 3; // Dependencies & NuGet
                IsSideBarVisible = true;
                break;
            case 2:
                SelectedBottomTabIndex = 3; // Test Cases
                IsBottomDeckExpanded = true;
                break;
        }
    }

    // ── VS Code Search in Script ──
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _replaceQuery = string.Empty;

    [ObservableProperty]
    private bool _searchMatchCase;

    [ObservableProperty]
    private bool _searchWholeWord;

    [ObservableProperty]
    private bool _searchUseRegex;

    [ObservableProperty]
    private string _searchStatusText = string.Empty;

    public ObservableCollection<SearchResultItem> SearchMatches { get; } = new();

    partial void OnSearchQueryChanged(string value) => ExecuteSearch();
    partial void OnSearchMatchCaseChanged(bool value) => ExecuteSearch();
    partial void OnSearchWholeWordChanged(bool value) => ExecuteSearch();
    partial void OnSearchUseRegexChanged(bool value) => ExecuteSearch();

    // ── VS Code Code Templates ──
    [ObservableProperty]
    private string _templateFilterQuery = string.Empty;

    public ObservableCollection<CodeTemplate> FilteredTemplates { get; } = new();
    private readonly List<CodeTemplate> _allTemplates = new();

    partial void OnTemplateFilterQueryChanged(string value) => RefreshFilteredTemplates();

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

        _allTemplates.AddRange(CodeTemplateLibrary.GetTemplates());
        RefreshFilteredTemplates();

        OpenTabs.Add(CreateTab(script, isActive: true));

        TriggerDiagnosticsCheck();
        PopulateExplorerTree();
    }

    private StudioTabItemViewModel CreateTab(ScriptDocumentItem document, bool isActive = false)
    {
        var tab = new StudioTabItemViewModel(document, isActive)
        {
            OnSelect = t => _ = SwitchToTabAsync(t),
            OnClose = t => _ = CloseTabAsync(t)
        };
        return tab;
    }

    public async Task SwitchToTabAsync(StudioTabItemViewModel tab)
    {
        if (tab.Id == Script.Id && tab.IsActive) return;

        // Save active script state
        Script.Code = Code;
        Script.Notes = Notes;

        foreach (var t in OpenTabs)
        {
            t.IsActive = (t.Id == tab.Id);
        }

        await UpdateActiveScriptAsync(tab.Document);
    }

    public async Task CloseTabAsync(StudioTabItemViewModel tab)
    {
        if (OpenTabs.Count <= 1)
        {
            return;
        }

        int index = OpenTabs.IndexOf(tab);
        OpenTabs.Remove(tab);

        if (tab.IsActive)
        {
            var nextTab = (index < OpenTabs.Count) ? OpenTabs[index] : OpenTabs[^1];
            await SwitchToTabAsync(nextTab);
        }
    }

    private void AddOrActivateTab(ScriptDocumentItem document)
    {
        var existing = OpenTabs.FirstOrDefault(t => t.Id == document.Id);
        if (existing != null)
        {
            foreach (var t in OpenTabs)
            {
                t.IsActive = (t.Id == document.Id);
            }
        }
        else
        {
            foreach (var t in OpenTabs)
            {
                t.IsActive = false;
            }
            var newTab = CreateTab(document, isActive: true);
            OpenTabs.Add(newTab);
        }
    }

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

        AddOrActivateTab(script);

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
        var activeTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (activeTab != null) activeTab.IsDirty = true;
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
                    SelectedBottomTabIndex = 0;
                }
            });
        });
        using var cancellationScope = InteractiveCancellationContext.EnterScope(token);

        try
        {
            if (CurrentLanguageMode == ExecutionLanguageMode.Statements || CurrentLanguageMode == ExecutionLanguageMode.Expression)
            {
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
                    ConsoleOutput += "❌ Compilation failed. Check the Problems tab for details.\n";
                    foreach (var diag in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        ConsoleOutput += $"  • {diag.LocationString}: {diag.Id} {diag.Message}\n";
                    }
                    CompilerStatusText = "Build Failed";
                    SelectedBottomTabIndex = 2;
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

        var activeTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (activeTab != null)
        {
            activeTab.IsDirty = false;
            activeTab.NotifyTitleChanged();
        }
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
    public void ToggleBottomDeck()
    {
        IsBottomDeckExpanded = !IsBottomDeckExpanded;
    }

    [RelayCommand]
    public void ToggleSideBar()
    {
        IsSideBarVisible = !IsSideBarVisible;
    }

    [RelayCommand]
    public void SelectActivityBarItem(string? indexStr)
    {
        if (int.TryParse(indexStr, out var index))
        {
            SelectActivityBarItem(index);
        }
    }

    public void SelectActivityBarItem(int index)
    {
        if (SelectedActivityBarIndex == index)
        {
            IsSideBarVisible = !IsSideBarVisible;
        }
        else
        {
            SelectedActivityBarIndex = index;
            IsSideBarVisible = true;
        }
    }

    [RelayCommand]
    public void ExecuteSearch()
    {
        SearchMatches.Clear();
        if (string.IsNullOrEmpty(SearchQuery))
        {
            SearchStatusText = string.Empty;
            return;
        }

        var lines = (Code ?? string.Empty).Split('\n');
        var query = SearchQuery;
        var comparison = SearchMatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            int idx = 0;
            while (idx < line.Length)
            {
                int foundIdx = line.IndexOf(query, idx, comparison);
                if (foundIdx < 0) break;

                SearchMatches.Add(new SearchResultItem
                {
                    LineNumber = i + 1,
                    Column = foundIdx + 1,
                    Length = query.Length,
                    LineText = line.Trim()
                });

                idx = foundIdx + Math.Max(1, query.Length);
            }
        }

        SearchStatusText = SearchMatches.Count == 1 ? "1 result" : $"{SearchMatches.Count} results";
    }

    [RelayCommand]
    public void NavigateToSearchMatch(SearchResultItem? match)
    {
        if (match == null) return;
        CaretLine = match.LineNumber;
        CaretColumn = match.Column;
        RequestNavigateToCaret?.Invoke(match.LineNumber, match.Column);
    }

    [RelayCommand]
    public void ReplaceNext()
    {
        if (string.IsNullOrEmpty(SearchQuery) || SearchMatches.Count == 0) return;
        var match = SearchMatches[0];
        var lines = (Code ?? string.Empty).Split('\n');
        if (match.LineNumber - 1 < lines.Length)
        {
            var line = lines[match.LineNumber - 1];
            var colIdx = match.Column - 1;
            if (colIdx >= 0 && colIdx + match.Length <= line.Length)
            {
                lines[match.LineNumber - 1] = line.Remove(colIdx, match.Length).Insert(colIdx, ReplaceQuery ?? string.Empty);
                Code = string.Join("\n", lines);
                ExecuteSearch();
            }
        }
    }

    [RelayCommand]
    public void ReplaceAll()
    {
        if (string.IsNullOrEmpty(SearchQuery)) return;
        if (SearchMatchCase)
        {
            Code = (Code ?? string.Empty).Replace(SearchQuery, ReplaceQuery ?? string.Empty);
        }
        else
        {
            Code = System.Text.RegularExpressions.Regex.Replace(
                Code ?? string.Empty,
                System.Text.RegularExpressions.Regex.Escape(SearchQuery),
                ReplaceQuery ?? string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        ExecuteSearch();
    }

    public void RefreshFilteredTemplates()
    {
        FilteredTemplates.Clear();
        var q = TemplateFilterQuery?.Trim();
        foreach (var t in _allTemplates)
        {
            if (string.IsNullOrEmpty(q) ||
                t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(q, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredTemplates.Add(t);
            }
        }
    }

    [RelayCommand]
    public void InsertTemplate(CodeTemplate? template)
    {
        if (template == null) return;
        if (string.IsNullOrWhiteSpace(Code))
        {
            Code = template.InitialCode;
            Notes = template.Notes;
        }
        else
        {
            Code += "\n\n" + template.InitialCode;
            if (!string.IsNullOrWhiteSpace(template.Notes))
            {
                Notes = string.IsNullOrWhiteSpace(Notes) ? template.Notes : Notes + "\n\n" + template.Notes;
            }
        }
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

    public void SetCaretPosition(int line, int col)
    {
        CaretLine = line;
        CaretColumn = col;
    }

    [RelayCommand]
    public async Task DebugCodeAsync()
    {
        if (IsExecuting || IsDebugging) return;

        DisposeRichOutputControls();
        DumpResults.Clear();
        RichOutputs.Clear();
        Locals.Clear();
        CallStack.Clear();
        GlobalVariableCache.Clear();
        SelectedBottomTabIndex = 4;
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
            SelectedBottomTabIndex = 2;
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

                SelectedBottomTabIndex = 4;
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
            GlobalVariableCache.Clear();
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

    [RelayCommand]
    public async Task OpenExternalProjectAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var result = await _storageService.OpenExternalProjectAsync(path);
            if (!result.Success)
            {
                CompilerStatusText = result.Message;
                return;
            }

            await RefreshExplorerAsync();

            if (!string.IsNullOrEmpty(result.PrimaryDocumentId))
            {
                var loaded = await _storageService.LoadScriptAsync(result.PrimaryDocumentId);
                if (loaded != null)
                {
                    await SaveAsync();
                    await UpdateActiveScriptAsync(loaded);
                }
            }

            CompilerStatusText = result.Message;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to open external project '{path}': {ex.Message}");
            CompilerStatusText = $"Error opening project: {ex.Message}";
        }
    }

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
