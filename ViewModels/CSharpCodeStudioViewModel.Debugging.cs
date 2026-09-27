using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpCodeStudioViewModel
{
    private IDebugSession? _activeDebugSession;

    [ObservableProperty]
    private bool _isDebugging;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _currentPausedLine = -1;

    public ObservableCollection<BreakpointItem> Breakpoints { get; } = new();
    public ObservableCollection<DebugVariableItem> Locals { get; } = new();
    public ObservableCollection<CallStackFrameItem> CallStack { get; } = new();

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

        if (!ActiveLanguage.Has(LanguageCapabilities.Debugging) || ActiveLanguage.Debugger == null)
        {
            if (ActiveLanguage.ScriptRunner != null)
            {
                await RunWithScriptRunnerAsync(debuggingTab, ActiveLanguage,
                    $"ℹ️ There's no {ActiveLanguage.DisplayName} debugger yet, so F5 runs the file without one.");
            }
            return;
        }

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
            debuggingTab.ConsoleOutput = $"🐞 Starting interactive {ActiveLanguage.DisplayName} debugging session...\n";
            debuggingTab.CompilerStatusText = "Preparing debugger...";
        }

        SelectedBottomTabIndex = 4;
        IsBottomDeckExpanded = true;
        ConsoleOutput = $"🐞 Starting interactive {ActiveLanguage.DisplayName} debugging session...\n";
        CompilerStatusText = "Preparing debugger...";
        IsExecuting = true;
        IsDebugging = true;
        IsPaused = false;
        CurrentPausedLine = -1;

        debuggingTab?.ExecutionCts?.Cancel();
        _executionCts?.Cancel();
        _executionCts = new CancellationTokenSource();
        if (debuggingTab != null) debuggingTab.ExecutionCts = _executionCts;
        var token = _executionCts.Token;

        var sourcePath = debuggingTab?.Document.SourceFilePath
            ?? (Script.Title.Contains('.') ? Script.Title : $"{Script.Title}{ActiveLanguage.FileExtensions.FirstOrDefault() ?? ".cs"}");

        var launchContext = new DebugLaunchContext(
            Script.Id,
            sourcePath,
            Code,
            Breakpoints.ToList(),
            Toolchain: null,
            liveText => Dispatcher.UIThread.Post(() => AppendLiveDebugOutput(liveText, debuggingTab)),
            token);

        var sw = Stopwatch.StartNew();

        try
        {
            var session = await Task.Run(() => ActiveLanguage.Debugger.LaunchAsync(launchContext, token), token);
            _activeDebugSession = session;

            session.Paused += args => Dispatcher.UIThread.Post(() => HandleSessionPaused(args, debuggingTab));
            session.Resumed += () => Dispatcher.UIThread.Post(() => HandleSessionResumed(debuggingTab));
            session.OutputReceived += text => Dispatcher.UIThread.Post(() => AppendLiveDebugOutput(text, debuggingTab));
            session.Terminated += args => Dispatcher.UIThread.Post(() => HandleSessionTerminated(args, debuggingTab, sw));

            var readyMsg = $"[Debug] {ActiveLanguage.DisplayName} debug session attached.\n--------------------------------------------------\n";
            AppendLiveDebugOutput(readyMsg, debuggingTab);
            CompilerStatusText = "Debugging...";
            if (debuggingTab != null) debuggingTab.CompilerStatusText = "Debugging...";
        }
        catch (DebugCompilationException ex)
        {
            HandleCompilationFailure(ex.Diagnostics, debuggingTab);
        }
        catch (Exception ex)
        {
            var errorMsg = $"\n❌ Failed to launch debugger: {ex.Message}\n";
            AppendLiveDebugOutput(errorMsg, debuggingTab);
            CompilerStatusText = "Debug Launch Failed";
            IsExecuting = false;
            IsDebugging = false;
            if (debuggingTab != null)
            {
                debuggingTab.CompilerStatusText = "Debug Launch Failed";
                debuggingTab.IsExecuting = false;
                debuggingTab.IsDebugging = false;
            }
        }
    }

    private void HandleCompilationFailure(IReadOnlyList<DiagnosticItem> diagnostics, StudioTabItemViewModel? debuggingTab)
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
    }

    private void AppendLiveDebugOutput(string text, StudioTabItemViewModel? tab)
    {
        if (tab != null)
        {
            tab.ConsoleOutput += text;
            if (tab.IsActive) ConsoleOutput = tab.ConsoleOutput;
        }
        else
        {
            ConsoleOutput += text;
        }
    }

    private void HandleSessionPaused(DebugPausedEventArgs args, StudioTabItemViewModel? tab)
    {
        int line = args.LineNumber;

        if (tab != null)
        {
            tab.IsPaused = true;
            tab.PausedLine = line;
            tab.CompilerStatusText = $"⏸️ Paused at Line {line} ({args.Reason})";
            tab.Locals.Clear();
            foreach (var l in args.Locals) tab.Locals.Add(l);
            tab.CallStack.Clear();
            foreach (var f in args.CallStack) tab.CallStack.Add(f);
        }

        if (tab == null || tab.IsActive)
        {
            IsPaused = true;
            CurrentPausedLine = line;
            CompilerStatusText = $"⏸️ Paused at Line {line} ({args.Reason})";

            Locals.Clear();
            foreach (var l in args.Locals) Locals.Add(l);

            CallStack.Clear();
            foreach (var f in args.CallStack) CallStack.Add(f);

            _ = UpdateWatchExpressionsAsync();

            SelectedBottomTabIndex = 4;
            IsBottomDeckExpanded = true;

            RequestSetPausedLine?.Invoke(line);
            RequestNavigateToCaret?.Invoke(line, 1);
        }
    }

    private void HandleSessionResumed(StudioTabItemViewModel? tab)
    {
        if (tab != null)
        {
            tab.IsPaused = false;
            tab.PausedLine = -1;
            tab.CompilerStatusText = "Debugging...";
        }

        if (tab == null || tab.IsActive)
        {
            IsPaused = false;
            CurrentPausedLine = -1;
            CompilerStatusText = "Debugging...";
            RequestSetPausedLine?.Invoke(-1);
        }
    }

    private void HandleSessionTerminated(DebugTerminatedEventArgs args, StudioTabItemViewModel? tab, Stopwatch sw)
    {
        sw.Stop();
        var timeText = $"{sw.Elapsed.TotalMilliseconds:N0} ms";
        var border = "\n--------------------------------------------------\n";

        string endMsg = args.WasCancelled
            ? $"{border}🛑 Debug session stopped.\n"
            : $"{border}🏁 Debugging finished in {timeText}\n";

        AppendLiveDebugOutput(endMsg, tab);

        if (tab != null)
        {
            tab.IsExecuting = false;
            tab.IsDebugging = false;
            tab.IsPaused = false;
            tab.PausedLine = -1;
            tab.ExecutionTimeText = timeText;
            tab.CompilerStatusText = args.WasCancelled ? "Stopped" : "Completed";
        }

        if (tab == null || tab.IsActive)
        {
            IsExecuting = false;
            IsDebugging = false;
            IsPaused = false;
            CurrentPausedLine = -1;
            ExecutionTimeText = timeText;
            CompilerStatusText = args.WasCancelled ? "Stopped" : "Completed";
            RequestSetPausedLine?.Invoke(-1);
        }

        _activeDebugSession = null;
    }

    [RelayCommand]
    public void ContinueDebug()
    {
        _ = _activeDebugSession?.ContinueAsync() ?? Task.CompletedTask;
    }

    [RelayCommand]
    public void StepOver()
    {
        _ = _activeDebugSession?.StepOverAsync() ?? Task.CompletedTask;
    }

    [RelayCommand]
    public void StepInto()
    {
        _ = _activeDebugSession?.StepIntoAsync() ?? Task.CompletedTask;
    }

    [RelayCommand]
    public void StepOut()
    {
        _ = _activeDebugSession?.StepOutAsync() ?? Task.CompletedTask;
    }

    [RelayCommand]
    public void StopDebug()
    {
        var targetTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        targetTab?.ExecutionCts?.Cancel();
        _executionCts?.Cancel();

        if (_activeDebugSession != null)
        {
            _ = _activeDebugSession.StopAsync();
        }
        else
        {
            ScriptDebugSession.Current?.Stop();
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
        if (!SupportsBreakpoints) return;
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
        foreach (var b in sorted) Breakpoints.Add(b);

        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
        _ = _storageService.SaveScriptAsync(Script);

        var currentTab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        var sourcePath = currentTab?.Document.SourceFilePath ?? (Script.Title.EndsWith(".cs") ? Script.Title : $"{Script.Title}.cs");
        _ = _activeDebugSession?.SetBreakpointsAsync(sourcePath, Breakpoints.ToList());
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
    }
}
