using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// Running a source file with its language's toolchain (a <c>.py</c> with Python): the file is saved, the toolchain
/// found, the language's run plan started as real processes with their output in the Terminal and what's typed there
/// sent to the program, and a failure's errors put in Problems, with a fix such as "Install numpy" when one applies.
/// Nothing here knows which language it runs.
/// </summary>
public partial class CSharpCodeStudioViewModel
{
    /// <summary>What's typed into the Terminal's input line for the running program.</summary>
    [ObservableProperty]
    private string _programInputText = string.Empty;

    /// <summary>True while the active tab's program is running and reads standard input.</summary>
    [ObservableProperty]
    private bool _isAcceptingProgramInput;

    /// <param name="note">A line printed before the run, e.g. why F5 ran without the debugger.</param>
    private async Task RunWithScriptRunnerAsync(StudioTabItemViewModel? runningTab, ILanguageDefinition language, string? note = null)
    {
        var document = Script;
        var runner = language.ScriptRunner!;
        var toolchains = language.Toolchain;

        // Run always runs what's in the editor, like Ctrl+S then run in a terminal.
        await SaveDocumentAsync(userAsked: true);
        var sourceFile = document.SourceFilePath;
        if (sourceFile == null || !File.Exists(sourceFile))
        {
            CompilerStatusText = $"⚠️ Save {document.Title} as a file before running it.";
            return;
        }

        var folder = Path.GetDirectoryName(sourceFile)!;
        var cts = new CancellationTokenSource();
        var timeoutSeconds = _getTimeoutSeconds();
        using var timeoutCts = timeoutSeconds > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)) : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);
        var token = linked.Token;

        // The Terminal shows a header, then what the program prints as a terminal would (progress bars redraw in place).
        var buffer = new TerminalTextBuffer();
        var header = note == null ? string.Empty : note + "\n";
        var footer = string.Empty;
        var refreshQueued = 0;
        void ShowNow()
        {
            string text;
            lock (buffer) text = header + buffer.Text + footer;
            if (runningTab != null) runningTab.ConsoleOutput = text;
            if (runningTab == null || runningTab.IsActive) ConsoleOutput = text;
        }
        void Show()
        {
            // Many small writes become one UI update.
            if (Interlocked.Exchange(ref refreshQueued, 1) == 1) return;
            _postToUiThread(() =>
            {
                Interlocked.Exchange(ref refreshQueued, 0);
                ShowNow();
            });
        }
        void Receive(string text)
        {
            lock (buffer) buffer.Append(text);
            Show();
        }

        if (runningTab != null)
        {
            runningTab.IsExecuting = true;
            runningTab.ExecutionCts = cts;
            runningTab.DumpResults.Clear();
            runningTab.RichOutputs.Clear();
            runningTab.Diagnostics.Clear();
            runningTab.AppendToConsole = Receive;
        }

        DisposeRichOutputControls();
        DumpResults.Clear();
        RichOutputs.Clear();
        Diagnostics.Clear();
        ErrorCount = 0;
        WarningCount = 0;
        SelectedBottomTabIndex = 1;
        IsBottomDeckExpanded = true;
        ExecutionTimeText = string.Empty;
        CompilerStatusText = $"Starting {language.DisplayName}…";
        IsExecuting = true;
        ShowNow();

        var status = "Completed";
        var elapsed = TimeSpan.Zero;
        try
        {
            ToolchainResolution resolution = toolchains == null
                ? ToolchainResolution.NotFound(new MissingToolchainGuidance($"{language.DisplayName} has no toolchain", string.Empty, Array.Empty<string>()))
                : await Task.Run(() => toolchains.ResolveAsync(new ToolchainQuery(folder, _storageService.ActiveWorkspaceRootPath), token), token);
            if (resolution.Toolchain is not { } toolchain)
            {
                var guidance = resolution.Missing ?? new MissingToolchainGuidance($"{language.DisplayName} can't run here", string.Empty, Array.Empty<string>());
                header += "❌ " + guidance.ToText();
                ShowNow();
                status = $"{toolchains?.ToolName ?? language.DisplayName} not found";
                ToolchainLabel = status;
                IsToolchainMissing = true;
                return;
            }

            ToolchainLabel = toolchain.Label;
            IsToolchainMissing = false;
            var plan = await runner.PlanAsync(new ScriptRunContext(sourceFile, folder, toolchain), token);
            header += $"▶ {toolchain.Label} · {Path.GetFileName(sourceFile)}\n";
            CompilerStatusText = "Running…";
            ShowNow();

            var session = new ScriptRunExecutor(_languages.Processes).Start(plan, sourceFile, language.RunDiagnostics, Receive, token);
            if (runningTab != null) runningTab.ActiveRun = session;
            if (language.Has(LanguageCapabilities.StandardInput) && (runningTab == null || runningTab.IsActive)) IsAcceptingProgramInput = true;

            var result = await session.Completion;
            elapsed = result.Elapsed;
            if (runningTab != null) runningTab.ActiveRun = null;

            var seconds = $"{result.Elapsed.TotalSeconds:0.00} s";
            if (result.WasCancelled)
            {
                var timedOut = timeoutCts.IsCancellationRequested;
                footer = timedOut ? $"\n⏱️ Stopped after {timeoutSeconds}s (the execution time limit).\n" : "\n🛑 Stopped.\n";
                status = timedOut ? $"⏱️ Timed out after {timeoutSeconds}s" : "🛑 Cancelled";
            }
            else if (result.StartError != null)
            {
                footer = $"\n❌ {result.StartError}\n";
                status = "Couldn't start";
            }
            else if (result.FailedBuildStep != null)
            {
                footer = $"\n❌ {result.FailedBuildStep} failed (exit code {result.ExitCode}).\n";
                status = "Build Failed";
            }
            else
            {
                footer = $"\n— exited with code {result.ExitCode} in {seconds}\n";
                status = result.ExitCode == 0 ? "Completed" : $"Exited with code {result.ExitCode}";
            }

            ShowProblems(runningTab, language, toolchain, result.Diagnostics);
        }
        catch (OperationCanceledException)
        {
            footer = "\n🛑 Stopped.\n";
            status = "🛑 Cancelled";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Running {sourceFile} failed: {ex}");
            footer = $"\n❌ {ex.Message}\n";
            status = "Run failed";
        }
        finally
        {
            ShowNow();
            var timeText = elapsed > TimeSpan.Zero ? $"{elapsed.TotalMilliseconds:N0} ms" : string.Empty;
            if (runningTab != null)
            {
                runningTab.ActiveRun = null;
                runningTab.AppendToConsole = null;
                runningTab.IsExecuting = false;
                runningTab.ExecutionTimeText = timeText;
                runningTab.CompilerStatusText = status;
                if (ReferenceEquals(runningTab.ExecutionCts, cts)) runningTab.ExecutionCts = null;
            }

            if (runningTab == null || runningTab.IsActive)
            {
                IsExecuting = false;
                IsAcceptingProgramInput = false;
                ExecutionTimeText = timeText;
                CompilerStatusText = status;
            }

            cts.Dispose();
        }
    }

    // A failed run's errors in Problems (the tab's and, if it's active, the panel's), with a fix for a missing dependency.
    private void ShowProblems(StudioTabItemViewModel? runningTab, ILanguageDefinition language, ToolchainInfo toolchain, DiagnosticParseResult diagnostics)
    {
        var problems = new List<DiagnosticItemViewModel>();
        foreach (var item in diagnostics.Diagnostics)
        {
            var package = diagnostics.MissingDependency != null && language.Packages is { } packages
                ? packages.PackageForMissingDependency(diagnostics.MissingDependency)
                : null;
            problems.Add(new DiagnosticItemViewModel(item, (line, column) =>
            {
                SetCaretPosition(line, column);
                RequestNavigateToCaret?.Invoke(line, column);
            })
            {
                QuickFixLabel = package == null ? null : $"Install {package}",
                QuickFixCommand = package == null ? null : new AsyncRelayCommand(() => InstallPackageAsync(language, toolchain, package))
            });
        }

        if (runningTab != null)
        {
            runningTab.Diagnostics.Clear();
            foreach (var problem in problems) runningTab.Diagnostics.Add(problem);
        }

        if (runningTab == null || runningTab.IsActive)
        {
            Diagnostics.Clear();
            foreach (var problem in problems) Diagnostics.Add(problem);
            ErrorCount = problems.Count(p => p.Severity == DiagnosticSeverity.Error);
            WarningCount = problems.Count(p => p.Severity == DiagnosticSeverity.Warning);
            if (problems.Count > 0) SelectedBottomTabIndex = 2;
        }
    }

    /// <summary>Installs a package the run was missing, showing the package manager's output in the Terminal.</summary>
    private async Task InstallPackageAsync(ILanguageDefinition language, ToolchainInfo toolchain, string package)
    {
        var packages = language.Packages;
        if (packages == null) return;

        var tab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        var command = packages.InstallCommand(package);
        void Append(string text) => _postToUiThread(() =>
        {
            if (tab != null) tab.ConsoleOutput += text;
            if (tab == null || tab.IsActive) ConsoleOutput += text;
        });

        ShowConsoleTab();
        Append($"\n$ {command.Text}\n");
        CompilerStatusText = $"Installing {package}…";
        var result = await Task.Run(() => packages.RunAsync(command, toolchain, Append));
        if (result.SwitchedToolchain != null) ToolchainLabel = result.SwitchedToolchain.Label;
        CompilerStatusText = result.Success ? $"Installed {package}: run again (F5)" : $"⚠️ Couldn't install {package}";
        Append(result.Success ? $"✅ Installed {package}. Run again (F5).\n" : $"❌ {result.Message}\n");
    }

    /// <summary>Sends the Terminal's input line to the running program, and shows it as a terminal would.</summary>
    [RelayCommand]
    public async Task SendProgramInputAsync()
    {
        var tab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (tab?.ActiveRun is not { AcceptsInput: true } run) return;

        var line = ProgramInputText;
        ProgramInputText = string.Empty;
        tab.AppendToConsole?.Invoke(line + "\n");
        await run.SendInputAsync(line + "\n");
    }

    // A closed tab's run doesn't go on unseen.
    private static void StopTabRun(StudioTabItemViewModel tab)
    {
        tab.ActiveRun?.Stop();
        try
        {
            tab.ExecutionCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
