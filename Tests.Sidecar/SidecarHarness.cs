using FrySharp.Core.Tests;
using FrySharp.Sidecar.Hosting;
using FrySharp.Sidecar.Protocol;
using FrySharp.Sidecar.Services;
using FrySharp.Sidecar.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Nerdbank.Streams;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using StreamJsonRpc;

namespace FrySharp.Sidecar.Tests;

public sealed class SidecarHarness : IAsyncDisposable
{
    public TempFolder Workspace { get; }
    public JsonRpc ClientRpc { get; }
    public JsonRpc ServerRpc { get; }
    public List<(string Method, object? Payload)> Notifications { get; }

    /// <summary>What the client answers when a cell asks for input; the requests it got.</summary>
    public Func<InputRequest, string?> InputAnswer { get; set; } = _ => null;
    public List<InputRequest> InputRequests { get; } = [];
 
    private SidecarHarness(TempFolder workspace, JsonRpc clientRpc, JsonRpc serverRpc, List<(string Method, object? Payload)> notifications)
    {
        Workspace = workspace;
        ClientRpc = clientRpc;
        ServerRpc = serverRpc;
        Notifications = notifications;
    }

    public static async Task<SidecarHarness> CreateAsync()
    {
        ScriptHost.Configure(ScriptHostProfile.Headless);
        var workspace = new TempFolder();
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();

        var serverHandler = new HeaderDelimitedMessageHandler(serverStream, serverStream, new JsonMessageFormatter());
        var serverRpc = new JsonRpc(serverHandler);

        var options = new SidecarOptions { WorkspaceRoot = workspace.Path };
        var engines = LanguageEngineServices.Create(options.DataDirectory);
        var languages = HeadlessLanguages.Create(engines);
        var compiler = new RoslynCompilerService();
        var runner = new FileRunner(languages, compiler, engines.Processes);

        var workspaceService = new WorkspaceService(options, NullLogger<WorkspaceService>.Instance);
        var languageFeatures = new LanguageFeatures(languages, compiler, NullLogger<LanguageFeatures>.Instance);
        var documents = new DocumentService(options, workspaceService, languageFeatures);
        var runs = new RunService(languages, workspaceService, runner, NullLogger<RunService>.Instance);
        var notebooks = new NotebookService(languages, workspaceService, NullLogger<NotebookService>.Instance);

        var sidecarTarget = new SidecarRpc(
            serverRpc,
            options,
            workspaceService,
            documents,
            languageFeatures,
            languages,
            runs,
            notebooks,
            new EngineInfoService(languages, workspaceService),
            NullLogger.Instance);

        serverRpc.AddLocalRpcTarget(sidecarTarget);
        serverRpc.StartListening();

        var harnessNotifications = new List<(string, object?)>();
        var clientHandler = new HeaderDelimitedMessageHandler(clientStream, clientStream, new JsonMessageFormatter());
        var clientRpc = new JsonRpc(clientHandler);

        SidecarHarness? created = null;
        clientRpc.AddLocalRpcTarget(new ClientReceiver(harnessNotifications, request =>
        {
            created!.InputRequests.Add(request);
            return created.InputAnswer(request);
        }));
        clientRpc.StartListening();

        var harness = created = new SidecarHarness(workspace, clientRpc, serverRpc, harnessNotifications);

        // Perform initialize handshake
        await clientRpc.InvokeWithParameterObjectAsync<InitializeResult>("fry/initialize", new InitializeParams("test-client"));

        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        ClientRpc.Dispose();
        ServerRpc.Dispose();
        Workspace.Dispose();
    }

    private sealed class ClientReceiver(List<(string, object?)> notifications, Func<InputRequest, string?> onInput)
    {
        [JsonRpcMethod(ClientMethods.InputRequest, UseSingleObjectParameterDeserialization = true)]
        public string? OnInputRequest(InputRequest request) => onInput(request);

        [JsonRpcMethod(ClientMethods.NotebookOutput, UseSingleObjectParameterDeserialization = true)]
        public void OnNotebookOutput(NotebookOutputNotification n) { lock (notifications) notifications.Add((ClientMethods.NotebookOutput, n)); }

        [JsonRpcMethod(ClientMethods.NotebookCellState, UseSingleObjectParameterDeserialization = true)]
        public void OnNotebookCellState(CellStateNotification n) { lock (notifications) notifications.Add((ClientMethods.NotebookCellState, n)); }

        [JsonRpcMethod(ClientMethods.RunOutput, UseSingleObjectParameterDeserialization = true)]
        public void OnRunOutput(RunOutputNotification n) => notifications.Add((ClientMethods.RunOutput, n));

        [JsonRpcMethod(ClientMethods.RunExited, UseSingleObjectParameterDeserialization = true)]
        public void OnRunExited(RunExitedNotification n) => notifications.Add((ClientMethods.RunExited, n));
    }
}
