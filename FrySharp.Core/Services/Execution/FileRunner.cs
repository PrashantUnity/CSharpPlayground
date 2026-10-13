using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Execution;

/// <summary>What a file run reports, and how its input reaches it.</summary>
public sealed class FileRunOptions
{
    /// <summary>Everything the run prints (and its build steps), in order, on background threads.</summary>
    public required Action<string> OnOutput { get; init; }

    /// <summary>Displays (tables, images, charts…) a C# run makes; dropped when null.</summary>
    public Action<RichCellOutput>? OnDisplay { get; init; }

    /// <summary>What C# code reads from <c>Console.In</c>; nothing (end of input) when null.</summary>
    public TextReader? CSharpInput { get; init; }

    /// <summary>The program of another language has started: input for it goes to <see cref="ScriptRunSession.SendInputAsync"/>.</summary>
    public Action<ScriptRunSession>? OnProcessStarted { get; init; }

    /// <summary>Arguments for the program, each passed as its own argument.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>The workspace the file belongs to, where toolchains look for a project's own environment.</summary>
    public string? WorkspaceRoot { get; init; }
}

/// <summary>How a file run ended: a process exit code (0 ran fine, 130 stopped, 127 couldn't start) and its problems.</summary>
public sealed record FileRunResult(int ExitCode, IReadOnlyList<DiagnosticItem> Problems);

/// <summary>
/// Runs a source file the way the studio does (F5), for any host: C# in this process (<see cref="InProcessCSharpRunner"/>),
/// any other language through its engine (its toolchain, then <see cref="IScriptRunner.PlanAsync"/>, then
/// <see cref="ScriptRunExecutor"/>), with the build steps and program the desktop runs, arguments never joined into one string.
/// </summary>
public sealed class FileRunner(LanguageRegistry<ILanguageEngine> languages, RoslynCompilerService compiler, IProcessLauncher processes)
{
    /// <summary>True when <paramref name="language"/> can run files here.</summary>
    public static bool CanRun(ILanguageEngine language) =>
        language.Id == LanguageIds.CSharp || (language.ScriptRunner != null && language.Toolchain != null);

    /// <summary>Runs the file at <paramref name="fullPath"/> as <paramref name="language"/>.</summary>
    /// <exception cref="NotSupportedException">The language can't run files here.</exception>
    public async Task<FileRunResult> RunFileAsync(string fullPath, ILanguageEngine language, FileRunOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (!CanRun(language)) throw new NotSupportedException($"{language.DisplayName} files can't be run here.");

        if (language.Id == LanguageIds.CSharp)
        {
            var code = await File.ReadAllTextAsync(fullPath, ct).ConfigureAwait(false);
            return await RunCSharpAsync(code, options, ct).ConfigureAwait(false);
        }

        var folder = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        var resolution = await language.Toolchain!.ResolveAsync(new ToolchainQuery(folder, options.WorkspaceRoot), ct).ConfigureAwait(false);
        if (resolution.Toolchain is not { } toolchain)
        {
            options.OnOutput((resolution.Missing?.ToText() ?? $"{language.Toolchain.ToolName} wasn't found.") + "\n");
            return new FileRunResult(127, []);
        }

        var plan = await language.ScriptRunner!.PlanAsync(new ScriptRunContext(fullPath, folder, toolchain), ct).ConfigureAwait(false);
        if (options.Arguments.Count > 0 && plan.Steps.Count > 0)
        {
            // Arguments are for the program, the last step: appended as arguments of their own.
            var last = plan.Steps[^1];
            plan = new ScriptRunPlan([
                .. plan.Steps.Take(plan.Steps.Count - 1),
                last with { Spec = last.Spec with { Arguments = [.. last.Spec.Arguments, .. options.Arguments] } }]);
        }

        var session = new ScriptRunExecutor(processes).Start(plan, fullPath, language.RunDiagnostics, options.OnOutput, ct);
        options.OnProcessStarted?.Invoke(session);
        var result = await session.Completion.ConfigureAwait(false);

        if (result.StartError is { } startError) options.OnOutput($"\n❌ {startError}\n");
        if (result.FailedBuildStep is { } failedStep) options.OnOutput($"\n❌ {failedStep} failed.\n");

        var exitCode = result.WasCancelled ? 130 : result.StartError != null ? 127 : result.ExitCode ?? 1;
        return new FileRunResult(exitCode, result.Diagnostics.Diagnostics);
    }

    /// <summary>Runs C# code (a file's text, or code typed without a file).</summary>
    public async Task<FileRunResult> RunCSharpAsync(string code, FileRunOptions options, CancellationToken ct)
    {
        var result = await new InProcessCSharpRunner(compiler).RunAsync(
            code,
            options.OnOutput,
            options.OnDisplay ?? (_ => { }),
            options.CSharpInput,
            ct).ConfigureAwait(false);

        // Compile and runtime errors are printed by the kernel and the engine as they happen; an abandoned run isn't.
        if (result.WasAbandoned && result.Error is { Length: > 0 } error) options.OnOutput($"\n⚠️ {error}\n");
        var problems = result.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
        return new FileRunResult(result.ExitCode, problems);
    }

    /// <summary>The language a file runs as, or null when no language here runs it.</summary>
    public ILanguageEngine? LanguageOf(string path) => languages.LanguageOf(path);
}
