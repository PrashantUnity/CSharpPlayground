using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

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
    private async Task RunWithScriptRunnerAsync(
        Common.StudioTabItemViewModel? runningTab,
        ILanguageDefinition language,
        string? note = null,
        IScriptRunner? overrideRunner = null,
        IToolchainProvider? overrideToolchains = null)
    {
        var document = Script;
        var runner = overrideRunner ?? language.ScriptRunner;
        var toolchains = overrideToolchains ?? language.Toolchain;

        if (runner == null)
        {
            CompilerStatusText = $"⚠️ {language.DisplayName} has no script runner configured.";
            return;
        }

        // Run always runs what's in the editor, like Ctrl+S then run in a terminal.
        await SaveDocumentAsync(userAsked: true);
        var sourceFile = document.SourceFilePath;
        if (sourceFile == null || !File.Exists(sourceFile))
        {
            try
            {
                var tempFolder = Path.Combine(Path.GetTempPath(), "FryStudio", "staged_scripts");
                Directory.CreateDirectory(tempFolder);
                var safeName = !string.IsNullOrWhiteSpace(document.Title) ? Path.GetFileNameWithoutExtension((string?)document.Title) : "Script";
                var ext = language.FileExtensions.FirstOrDefault() ?? ".cs";
                sourceFile = Path.Combine(tempFolder, $"{safeName}{ext}");
                await File.WriteAllTextAsync(sourceFile, Code);
            }
            catch
            {
                CompilerStatusText = $"⚠️ Save {document.Title} as a file before running it.";
                return;
            }
        }

        var folder = Path.GetDirectoryName((string?)sourceFile)!;

        var beforeHook = new FrySharp.Sdk.ExecutionHookContext
        {
            LanguageId = language.Id,
            DocumentPath = sourceFile,
            SourceCode = Code
        };
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.HookRegistry.InvokeBeforeScriptRun(beforeHook);
        if (beforeHook.CancelExecution)
        {
            var cancelReason = beforeHook.CancellationReason ?? "Execution cancelled by extension hook.";
            CompilerStatusText = "Cancelled by Hook";
            ConsoleOutput = $"⚠️ {cancelReason}\n";
            return;
        }

        var cts = new CancellationTokenSource();
        var timeoutSeconds = _getTimeoutSeconds();
        using var timeoutCts = timeoutSeconds > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)) : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);
        var token = linked.Token;

        // The Terminal shows a header, then what the program prints as a terminal would (progress bars redraw in place).
        var buffer = new TerminalTextBuffer();
        var header = note == null ? string.Empty : note + "\n";
        var footer = string.Empty;
        int? exitCode = null;
        var refreshQueued = 0;
        void ShowNow()
        {
            string text;
            string body;
            lock (buffer)
            {
                body = buffer.Text;
                text = header + body + footer;
            }
            if (runningTab != null)
            {
                runningTab.ConsoleHeader = header;
                runningTab.ConsoleBody = body;
                runningTab.ConsoleFooter = footer;
                runningTab.ConsoleExitCode = exitCode;
                runningTab.ConsoleOutput = text;
            }
            if (runningTab == null || runningTab.IsActive)
            {
                ConsoleHeader = header;
                ConsoleBody = body;
                ConsoleFooter = footer;
                ConsoleExitCode = exitCode;
                ConsoleOutput = text;
            }
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
        void ReceiveConsole(string text)
        {
            lock (buffer) buffer.Append(text);
            Show();
        }

        var richDumpCount = 0;
        void ReceiveRich(RichCellOutput rich)
        {
            // A display that couldn't be drawn (a spec with a mistake in it) says so in the Terminal.
            if (rich.Kind == CellOutputKind.Error)
            {
                ReceiveConsole($"⚠️ {rich.Text}\n");
                return;
            }

            if (rich.TableResult != null) Interlocked.Increment(ref richDumpCount);
            _postToUiThread(() =>
            {
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
                    else
                    {
                        SelectedBottomTabIndex = 0;
                    }
                    OnPropertyChanged(nameof(HasNoResults));
                }
            });
        }

        // The program's visuals, and the events on them that its code listens to (they reach it over the event socket).
        var visuals = new ExternalVisualSession();
        var outputProcessor = new ExternalOutputProcessor(ReceiveConsole, ReceiveRich, visuals: visuals.Visuals);

        if (runningTab != null)
        {
            runningTab.IsExecuting = true;
            runningTab.ExecutionCts = cts;
            runningTab.DumpResults.Clear();
            runningTab.RichOutputs.Clear();
            runningTab.Diagnostics.Clear();
            runningTab.AppendToConsole = outputProcessor.ProcessChunk;
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
            header += $"▶ {toolchain.Label} · {Path.GetFileName((string?)sourceFile)}\n";
            CompilerStatusText = "Running…";
            ShowNow();

            var session = new ScriptRunExecutor(_languages.Processes).Start(plan, sourceFile, language.RunDiagnostics, outputProcessor.ProcessChunk, token, visuals.Environment);
            if (runningTab != null) runningTab.ActiveRun = session;
            if (language.Has(LanguageCapabilities.StandardInput) && (runningTab == null || runningTab.IsActive)) IsAcceptingProgramInput = true;

            var result = await session.Completion;
            outputProcessor.Flush();
            elapsed = result.Elapsed;
            if (runningTab != null) runningTab.ActiveRun = null;

            var seconds = $"{result.Elapsed.TotalSeconds:0.00} s";
            exitCode = result.ExitCode;
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
                var dumpCount = Math.Max(richDumpCount, runningTab?.DumpResults.Count ?? DumpResults.Count);
                status = result.ExitCode == 0
                    ? (dumpCount > 0 ? $"Completed • {dumpCount} visual dump{(dumpCount == 1 ? "" : "s")}" : "Completed")
                    : $"Exited with code {result.ExitCode}";

                if (result.ExitCode == 0 && (dumpCount > 0 || (runningTab?.RichOutputs ?? RichOutputs).Any(IsDrawnInResults)))
                {
                    _postToUiThread(() =>
                    {
                        if (runningTab == null || runningTab.IsActive) SelectedBottomTabIndex = 0;
                    });
                }
            }

            ShowProblems(runningTab, language, toolchain, result.Diagnostics);
        }
        catch (OperationCanceledException)
        {
            exitCode = null;
            footer = "\n🛑 Stopped.\n";
            status = "🛑 Cancelled";
        }
        catch (Exception ex)
        {
            exitCode = 1;
            Debug.WriteLine($"[CSharpEditorPlugin] Running {sourceFile} failed: {ex}");
            footer = $"\n❌ {ex.Message}\n";
            status = "Run failed";
        }
        finally
        {
            visuals.Dispose(); // the program has ended: its visuals stay, disconnected
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

            PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.HookRegistry.InvokeAfterScriptRun(
                new FrySharp.Sdk.ExecutionFinishedHookContext
                {
                    LanguageId = language.Id,
                    DocumentPath = sourceFile,
                    Success = exitCode == 0,
                    Elapsed = elapsed,
                    Output = buffer.Text,
                    Error = exitCode != 0 ? footer : string.Empty
                });

            cts.Dispose();
        }
    }

    // A failed run's errors in Problems (the tab's and, if it's active, the panel's), with a fix for a missing dependency.
    private void ShowProblems(Common.StudioTabItemViewModel? runningTab, ILanguageDefinition language, ToolchainInfo toolchain, DiagnosticParseResult diagnostics)
    {
        var problems = new List<Common.DiagnosticItemViewModel>();
        foreach (var item in diagnostics.Diagnostics)
        {
            var package = diagnostics.MissingDependency != null && language.Packages is { } packages
                ? packages.PackageForMissingDependency(diagnostics.MissingDependency)
                : null;
            problems.Add(new Common.DiagnosticItemViewModel(item, (line, column) =>
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

        // A package that belongs to the file (a Rust crate) is only installed once the file names it.
        if (result.Success && result.DirectiveToInsert is { Length: > 0 } directive)
        {
            _postToUiThread(() =>
            {
                if (!Code.Contains(directive, StringComparison.Ordinal)) Code = directive + Environment.NewLine + Code;
                RefreshDocumentNuGetPackages();
            });
        }

        CompilerStatusText = result.Success ? $"Installed {package}: run again (F5)" : $"⚠️ Couldn't install {package}";
        Append(result.Success
            ? (result.DirectiveToInsert is { Length: > 0 } added
                ? $"✅ Installed {package}. Added `{added}` to the file. Run again (F5).\n"
                : $"✅ Installed {package}. Run again (F5).\n")
            : $"❌ {result.Message}\n");
    }

    /// <summary>Sends the Terminal's input line to the running program, and shows it as a terminal would.</summary>
    [RelayCommand]
    public async Task SendProgramInputAsync()
    {
        var tab = OpenTabs.FirstOrDefault(t => t.Id == Script.Id);
        if (tab == null) return;

        var line = ProgramInputText;
        ProgramInputText = string.Empty;
        tab.AppendToConsole?.Invoke(line + "\n");

        if (tab.ActiveRun is { } run)
        {
            await run.SendInputAsync(line + "\n");
        }
        else if (tab.InProcessStdin != null)
        {
            tab.InProcessStdin.PostInput(line);
        }
    }

    // A closed tab's run doesn't go on unseen.
    private static void StopTabRun(Common.StudioTabItemViewModel tab)
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
