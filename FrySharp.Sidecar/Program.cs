using System.CommandLine;
using FrySharp.Sidecar.Hosting;
using FrySharp.Sidecar.Services;
using FrySharp.Sidecar.Sessions;
using Microsoft.Extensions.Logging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using StreamJsonRpc;

namespace FrySharp.Sidecar;

/// <summary>
/// Entry point for the headless FrySharp sidecar process.
/// Communicates with the VS Code extension over stdio using JSON-RPC 2.0.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var workspaceOption = new Option<DirectoryInfo>("--workspace", "-w")
        {
            Description = "Folder the sidecar operates on.",
            DefaultValueFactory = _ => new DirectoryInfo(Environment.CurrentDirectory),
        };

        var root = new RootCommand("FrySharp Sidecar: JSON-RPC engine for the VS Code extension.")
        {
            workspaceOption
        };

        root.SetAction(async (parse, ct) =>
        {
            var workspaceDir = parse.GetValue(workspaceOption)!.FullName;
            await RunSidecarAsync(workspaceDir, ct).ConfigureAwait(false);
            return 0;
        });

        return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    public static async Task RunSidecarAsync(string workspaceRoot, CancellationToken ct)
    {
        ScriptHost.Configure(ScriptHostProfile.Headless);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace; // ALL logs to stderr!
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger("FrySharp.Sidecar");
        logger.LogInformation("Starting FrySharp sidecar in {Workspace}", workspaceRoot);

        var options = new SidecarOptions
        {
            WorkspaceRoot = workspaceRoot
        };

        var engines = LanguageEngineServices.Create(options.DataDirectory);
        var languages = HeadlessLanguages.Create(engines);
        var compiler = new RoslynCompilerService();
        var runner = new FileRunner(languages, compiler, engines.Processes);

        var workspace = new WorkspaceService(options, loggerFactory.CreateLogger<WorkspaceService>());
        var languageFeatures = new LanguageFeatures(languages, compiler, loggerFactory.CreateLogger<LanguageFeatures>());
        var documents = new DocumentService(options, workspace, languageFeatures);
        var runs = new RunService(languages, workspace, runner, loggerFactory.CreateLogger<RunService>());
        var notebooks = new NotebookService(languages, workspace, loggerFactory.CreateLogger<NotebookService>());

        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();
        var formatter = new JsonMessageFormatter();
        formatter.JsonSerializer.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        var handler = new HeaderDelimitedMessageHandler(stdout, stdin, formatter);
        using var jsonRpc = new JsonRpc(handler);

        var rpc = new SidecarRpc(
            jsonRpc,
            options,
            workspace,
            documents,
            languageFeatures,
            languages,
            runs,
            notebooks,
            new EngineInfoService(languages, workspace),
            logger);

        jsonRpc.AddLocalRpcTarget(rpc);
        jsonRpc.StartListening();

        logger.LogInformation("FrySharp sidecar listening on stdio.");
        await jsonRpc.Completion.ConfigureAwait(false);
        logger.LogInformation("FrySharp sidecar exiting.");
    }
}
