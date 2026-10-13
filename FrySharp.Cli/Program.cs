using System.CommandLine;
using System.Runtime.InteropServices;
using FrySharp.Sidecar.Hosting;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

namespace FrySharp.Cli;

/// <summary><c>frysharp</c>: run the VS Code sidecar, run a script, or show what the tool finds on this computer.</summary>
public static class Program
{
    public static Task<int> Main(string[] args) => BuildCommand().Parse(args).InvokeAsync();

    public static RootCommand BuildCommand()
    {
        var root = new RootCommand("FrySharp: polyglot notebook and Roslyn scripting engine.");
        root.Subcommands.Add(SidecarCommand());
        root.Subcommands.Add(RunCommand());
        root.Subcommands.Add(InfoCommand());
        return root;
    }

    private static Command SidecarCommand()
    {
        var workspace = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Folder the sidecar operates on (default: current directory).",
            DefaultValueFactory = _ => new DirectoryInfo(Environment.CurrentDirectory),
        };

        var command = new Command("sidecar", "Run the JSON-RPC stdio sidecar for the VS Code extension.") { workspace };
        command.SetAction(async (parse, ct) =>
        {
            var workspaceDir = parse.GetValue(workspace)!.FullName;
            await FrySharp.Sidecar.Program.RunSidecarAsync(workspaceDir, ct).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command RunCommand()
    {
        var file = new Argument<FileInfo>("file") { Description = "The script to run: .cs, .csx, .py or .js." };
        var arguments = new Argument<string[]>("arguments") { Description = "Arguments for the script (after --).", Arity = ArgumentArity.ZeroOrMore };
        file.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<FileInfo>() is { Exists: false } missing) result.AddError($"'{missing.FullName}' doesn't exist.");
        });

        var command = new Command("run", "Run a script here, the way the studio's Run does.") { file, arguments };
        command.SetAction(async (parse, ct) =>
        {
            ScriptHost.Configure(ScriptHostProfile.Headless);
            var output = parse.InvocationConfiguration.Output;
            var error = parse.InvocationConfiguration.Error;
            var path = parse.GetValue(file)!.FullName;
            var engines = LanguageEngineServices.Create(SidecarOptions.DefaultDataDirectory());
            var languages = HeadlessLanguages.Create(engines);
            var runner = new FileRunner(languages, new RoslynCompilerService(), engines.Processes);
            if (runner.LanguageOf(path) is not { } language || !FileRunner.CanRun(language))
            {
                await error.WriteLineAsync($"{Path.GetFileName(path)} isn't a script frysharp runs (.cs, .csx, .py, .js).");
                return 2;
            }

            ScriptRunSession? process = null;
            using var input = new CancellationTokenSource();
            var options = new FileRunOptions
            {
                OnOutput = output.Write,
                OnDisplay = display => output.WriteLine(Describe(display)),
                CSharpInput = Console.In,
                OnProcessStarted = started =>
                {
                    process = started;
                    _ = ForwardInputAsync(started, input.Token);
                },
                Arguments = parse.GetValue(arguments) ?? [],
                WorkspaceRoot = Environment.CurrentDirectory,
            };

            try
            {
                var result = await runner.RunFileAsync(path, language, options, ct);
                foreach (var problem in result.Problems)
                {
                    await error.WriteLineAsync($"{Path.GetFileName(path)}({problem.Line},{problem.Column}): {problem.Severity.ToString().ToLowerInvariant()} {problem.Id}: {problem.Message}");
                }
                return result.ExitCode;
            }
            finally
            {
                await input.CancelAsync();
                process?.Stop();
            }
        });
        return command;
    }

    private static Command InfoCommand()
    {
        var command = new Command("info", "Show the .NET runtime and the languages frysharp finds on this computer.");
        command.SetAction(async (parse, ct) =>
        {
            var output = parse.InvocationConfiguration.Output;
            await output.WriteLineAsync($"frysharp      {typeof(Program).Assembly.GetName().Version?.ToString(3)}");
            await output.WriteLineAsync($".NET          {Environment.Version} ({RuntimeInformation.RuntimeIdentifier})");
            await output.WriteLineAsync($"OS            {RuntimeInformation.OSDescription}");

            var languages = HeadlessLanguages.Create(LanguageEngineServices.Create(SidecarOptions.DefaultDataDirectory()));
            foreach (var language in languages.All)
            {
                if (language.Toolchain is not { } toolchains)
                {
                    await output.WriteLineAsync($"{language.DisplayName,-13} {language.RuntimeDescription}");
                    continue;
                }

                var found = await toolchains.ResolveAsync(new PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains.ToolchainQuery(Environment.CurrentDirectory), ct);
                await output.WriteLineAsync(found.Toolchain is { } toolchain
                    ? $"{language.DisplayName,-13} {toolchain.Version} ({toolchain.ExecutablePath})"
                    : $"{language.DisplayName,-13} not found");
            }
            return 0;
        });
        return command;
    }

    // A program of another language reads its input from what is typed here, line by line.
    private static async Task ForwardInputAsync(ScriptRunSession process, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && await Console.In.ReadLineAsync(ct) is { } line)
            {
                if (process.AcceptsInput) await process.SendInputAsync(line + "\n");
            }
        }
        catch (OperationCanceledException)
        {
            // The program ended.
        }
    }

    // A terminal shows Markdown as its text; for a table or a chart, it says what was shown.
    private static string Describe(RichCellOutput output)
    {
        var bundle = RichOutputMime.ToBundle(output, out _);
        if (bundle?["text/markdown"]?.GetValue<string>() is { } markdown) return markdown;
        return bundle?["text/plain"]?.GetValue<string>() is { Length: > 0 } text ? $"[{text}]" : "[display]";
    }
}
