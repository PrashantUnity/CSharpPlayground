using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

public partial class CSharpCodeStudioViewModel
{
    private IDebugSession? _activeDebugSession;
    private readonly DebugVariableChangeTracker _variableChangeTracker = new();

    public IDebugSession? ActiveDebugSession => _activeDebugSession;

    public async Task<IReadOnlyList<DebugVariableItem>> GetVariableChildrenAsync(DebugVariableItem item)
    {
        if (_activeDebugSession != null)
        {
            return await _activeDebugSession.GetVariableChildrenAsync(item);
        }
        return item.Children;
    }

    [ObservableProperty]
    private bool _isDebugging;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _currentPausedLine = -1;

    public ObservableCollection<DebugVariableItem> Locals { get; } = new();
    public ObservableCollection<CallStackFrameItem> CallStack { get; } = new();

    public event Action<int>? RequestSetPausedLine;

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

        // A source-file language is built from the file on disk, so what the editor shows has to be saved first, as Run does;
        // otherwise breakpoints are set on lines of code that isn't what runs.
        if (ActiveLanguage.Storage == LanguageStorageKind.SourceFile) await SaveDocumentAsync(userAsked: true);

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

        var sourcePath = DebugSourcePath(debuggingTab);

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

            if (session is DapDebugSession dapSession)
            {
                dapSession.BreakpointVerifiedChanged += (line, verified) => Dispatcher.UIThread.Post(() =>
                {
                    var bp = Breakpoints.FirstOrDefault(b => b.LineNumber == line);
                    if (bp != null)
                    {
                        bp.IsVerified = verified;
                        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));
                    }
                });
            }

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

    private void HandleCompilationFailure(IReadOnlyList<DiagnosticItem> diagnostics, Common.StudioTabItemViewModel? debuggingTab)
    {
        var failMsg = "❌ Debug compilation failed. Check the Problems tab for details.\n";
        if (debuggingTab != null)
        {
            debuggingTab.ConsoleOutput += failMsg;
            debuggingTab.Diagnostics.Clear();
            foreach (var d in diagnostics)
            {
                debuggingTab.Diagnostics.Add(new Common.DiagnosticItemViewModel(d, (l, c) => RequestNavigateToCaret?.Invoke(l, c)));
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
                Diagnostics.Add(new Common.DiagnosticItemViewModel(d, (l, c) => RequestNavigateToCaret?.Invoke(l, c)));
            }
            ErrorCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            WarningCount = Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
            SelectedBottomTabIndex = 2;
            CompilerStatusText = "Build Failed";
            IsExecuting = false;
            IsDebugging = false;
        }
    }

    private void AppendLiveDebugOutput(string text, Common.StudioTabItemViewModel? tab)
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

    private void HandleSessionPaused(DebugPausedEventArgs args, Common.StudioTabItemViewModel? tab)
    {
        int line = args.LineNumber;
        _variableChangeTracker.TrackAndMarkChanges(args.Locals);

        var status = line > 0
            ? $"⏸️ Paused at Line {line} ({args.Reason})"
            : $"⏸️ Paused ({args.Reason})";

        if (tab != null)
        {
            tab.IsPaused = true;
            tab.PausedLine = line > 0 ? line : -1;
            tab.CompilerStatusText = status;
            tab.Locals.Clear();
            foreach (var l in args.Locals) tab.Locals.Add(l);
            tab.CallStack.Clear();
            foreach (var f in args.CallStack) tab.CallStack.Add(f);
        }

        if (tab == null || tab.IsActive)
        {
            IsPaused = true;
            CurrentPausedLine = line > 0 ? line : -1;
            CompilerStatusText = status;

            Locals.Clear();
            foreach (var l in args.Locals) Locals.Add(l);

            CallStack.Clear();
            foreach (var f in args.CallStack) CallStack.Add(f);

            _ = UpdateWatchExpressionsAsync();

            SelectedBottomTabIndex = 4;
            IsBottomDeckExpanded = true;

            if (line > 0)
            {
                RequestSetPausedLine?.Invoke(line);
                RequestNavigateToCaret?.Invoke(line, 1);
            }
            else
            {
                RequestSetPausedLine?.Invoke(-1);
            }
        }
    }

    private void HandleSessionResumed(Common.StudioTabItemViewModel? tab)
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

    /// <summary>The file a debug session knows the script as; breakpoints set while it runs must use the same name.</summary>
    private string DebugSourcePath(Common.StudioTabItemViewModel? tab) =>
        tab?.Document.SourceFilePath
        ?? (Script.Title.Contains('.') ? Script.Title : $"{Script.Title}{ActiveLanguage.FileExtensions.FirstOrDefault() ?? ".cs"}");

    private void HandleSessionTerminated(DebugTerminatedEventArgs args, Common.StudioTabItemViewModel? tab, Stopwatch sw)
    {
        sw.Stop();
        var timeText = $"{sw.Elapsed.TotalMilliseconds:N0} ms";
        var border = "\n--------------------------------------------------\n";

        // -1 is "the adapter went away", not a program's answer.
        var exitCode = !args.WasCancelled && args.ExitCode is { } code && code != 0 && code != -1 ? code : (int?)null;
        var statusText = args.WasCancelled ? "Stopped" : exitCode is { } failed ? $"Exited with code {failed}" : "Completed";

        string endMsg = args.WasCancelled
            ? $"{border}🛑 Debug session stopped.\n"
            : exitCode is { } exited
                ? $"{border}🏁 Debugging finished in {timeText}: the program exited with code {exited}\n"
                : $"{border}🏁 Debugging finished in {timeText}\n";

        AppendLiveDebugOutput(endMsg, tab);

        if (tab != null)
        {
            tab.IsExecuting = false;
            tab.IsDebugging = false;
            tab.IsPaused = false;
            tab.PausedLine = -1;
            tab.ExecutionTimeText = timeText;
            tab.CompilerStatusText = statusText;
        }

        if (tab == null || tab.IsActive)
        {
            IsExecuting = false;
            IsDebugging = false;
            IsPaused = false;
            CurrentPausedLine = -1;
            ExecutionTimeText = timeText;
            CompilerStatusText = statusText;
            RequestSetPausedLine?.Invoke(-1);
        }

        foreach (var bp in Breakpoints) bp.IsVerified = true;
        RequestSyncBreakpoints?.Invoke(Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber));

        _activeDebugSession = null;
    }

    [RelayCommand]
    public void ContinueDebug()
    {
        if (!IsPaused || _activeDebugSession == null) return;
        IsPaused = false;
        _ = _activeDebugSession.ContinueAsync();
    }

    [RelayCommand]
    public void StepOver()
    {
        if (!IsPaused || _activeDebugSession == null) return;
        IsPaused = false;
        _ = _activeDebugSession.StepOverAsync();
    }

    [RelayCommand]
    public void StepInto()
    {
        if (!IsPaused || _activeDebugSession == null) return;
        IsPaused = false;
        _ = _activeDebugSession.StepIntoAsync();
    }

    [RelayCommand]
    public void StepOut()
    {
        if (!IsPaused || _activeDebugSession == null) return;
        IsPaused = false;
        _ = _activeDebugSession.StepOutAsync();
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
        // Start again once the old session has really ended: DebugCodeAsync does nothing while one is still running,
        // so a guessed pause made Restart silently do nothing whenever the stop took longer than the guess.
        if (await WaitUntilIdleAsync(RestartStopLimit))
        {
            await DebugCodeAsync();
        }
        else
        {
            CompilerStatusText = "The previous debug session has not stopped yet; restart once it has.";
        }
    }

    // How long Restart waits for the old session to stop before it gives up and says so.
    internal static TimeSpan RestartStopLimit { get; set; } = TimeSpan.FromSeconds(10);

    private async Task<bool> WaitUntilIdleAsync(TimeSpan limit)
    {
        if (!IsExecuting && !IsDebugging) return true;
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!IsExecuting && !IsDebugging) idle.TrySetResult();
        }

        PropertyChanged += OnChanged;
        try
        {
            if (!IsExecuting && !IsDebugging) return true;
            await idle.Task.WaitAsync(limit);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            PropertyChanged -= OnChanged;
        }
    }

    public event Action<DebugVariableItem>? RequestExploreVariable;
    public event Action<DebugVariableItem>? RequestViewVariable;

    [RelayCommand]
    public void ExploreVariable(DebugVariableItem? item)
    {
        if (item != null)
        {
            RequestExploreVariable?.Invoke(item);
        }
    }

    [RelayCommand]
    public void ViewVariable(DebugVariableItem? item)
    {
        if (item != null)
        {
            RequestViewVariable?.Invoke(item);
        }
    }

    [RelayCommand]
    public async Task ExpandVariableAsync(DebugVariableItem? item)
    {
        if (item == null) return;
        item.IsExpanded = !item.IsExpanded;
        if (item.IsExpanded && !item.ChildrenLoaded && item.HasChildren)
        {
            item.IsLoadingChildren = true;
            try
            {
                await GetVariableChildrenAsync(item);
            }
            finally
            {
                item.IsLoadingChildren = false;
            }
        }
    }
}
