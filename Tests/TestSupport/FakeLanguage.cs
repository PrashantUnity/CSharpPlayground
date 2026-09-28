using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

/// <summary>
/// A made-up language ("fakelang", <c>.fake</c> files) registered only in tests. It proves a language plugs in through
/// <see cref="ILanguageDefinition"/> alone: its toolchain is always found, its runner starts <c>fakec run &lt;file&gt;</c>
/// through whatever launcher the test supplies, and its kernel keeps variables in a dictionary.
/// </summary>
public sealed class FakeLanguage : LanguageDefinition
{
    public const string LanguageId = "fakelang";
    public const string Extension = ".fake";

    private static readonly string[] FakeAliases = ["fk"];
    private static readonly string[] FakeExtensions = [Extension];

    public FakeToolchainProvider FakeToolchain { get; } = new();
    public FakePackageManager FakePackages { get; } = new();
    public FakeKernelFactory Kernels { get; }
    public FakeDiagnosticParser Parser { get; } = new();

    public FakeLanguage()
    {
        Kernels = new FakeKernelFactory(FakePackages);
    }

    public override string Id => LanguageId;
    public override string DisplayName => "FakeLang";
    public override string ShortName => "FK";
    public override IReadOnlyList<string> Aliases => FakeAliases;
    public override IReadOnlyList<string> FileExtensions => FakeExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput | LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing | LanguageCapabilities.Packages;

    public override string IconKind => "LanguageJavascript";
    public override string AccentHex => "#F7DF1E";
    public override string LineCommentPrefix => "--";
    public override string NewFileTemplate => "print hello from fakelang\n";

    public override IToolchainProvider Toolchain => FakeToolchain;
    public override IScriptRunner ScriptRunner { get; } = new FakeRunner();
    public override IDiagnosticParser RunDiagnostics => Parser;
    public override INotebookKernelFactory NotebookKernels => Kernels;
    public override IPackageManager Packages => FakePackages;

    /// <summary>Registers a fresh fake language in <paramref name="services"/>'s registry and returns it.</summary>
    public static FakeLanguage RegisterIn(LanguageRegistry registry)
    {
        var language = new FakeLanguage();
        registry.Register(language);
        return language;
    }

    private sealed class FakeRunner : IScriptRunner
    {
        public Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default) =>
            Task.FromResult(new ScriptRunPlan(
            [
                new ProcessStep("Run", new ProcessStartSpec
                {
                    FileName = context.Toolchain.ExecutablePath,
                    Arguments = ["run", context.SourceFilePath],
                    WorkingDirectory = context.WorkingDirectory
                })
            ]));
    }
}

public sealed class FakeToolchainProvider : IToolchainProvider
{
    public bool Installed { get; set; } = true;
    public int ResolveCount;

    public ToolchainInfo Toolchain { get; } = new()
    {
        LanguageId = FakeLanguage.LanguageId,
        ExecutablePath = "/fake/bin/fakec",
        Version = new Version(1, 2, 3),
        DisplayName = "FakeLang 1.2.3",
        Source = "Test"
    };

    public string LanguageId => FakeLanguage.LanguageId;
    public string ToolName => "FakeLang";
    public string? SelectedPath { get; private set; }

    public IReadOnlyList<ToolchainAction> Actions { get; } =
        [new ToolchainAction("fake-setup", "Set up FakeLang", "Pretends to create an environment")];

    public Task<ToolchainResolution> ResolveAsync(ToolchainQuery query, CancellationToken ct = default)
    {
        Interlocked.Increment(ref ResolveCount);
        return Task.FromResult(Installed
            ? ToolchainResolution.Found(Toolchain)
            : ToolchainResolution.NotFound(new MissingToolchainGuidance(
                "FakeLang isn't installed",
                "Install FakeLang to run .fake files.",
                ["fakepkg install fakelang"],
                "https://example.invalid/fakelang")));
    }

    public Task<IReadOnlyList<ToolchainInfo>> ListAsync(ToolchainQuery query, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ToolchainInfo>>(Installed ? [Toolchain] : []);

    public void Select(string? executablePath) => SelectedPath = executablePath;

    public Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default)
    {
        output("setting up fakelang\n");
        return Task.FromResult(new ToolchainActionResult(true, "FakeLang is set up.", Toolchain));
    }

    public void Refresh()
    {
    }
}

/// <summary>Reports "Line N: message" lines of the output as errors, and "missing module X" as a missing dependency.</summary>
public sealed class FakeDiagnosticParser : IDiagnosticParser
{
    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticItem>();
        string? missing = null;
        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("Line ", StringComparison.Ordinal) && line.IndexOf(':') is var colon and > 5 &&
                int.TryParse(line[5..colon], out var number))
            {
                diagnostics.Add(new DiagnosticItem { Id = "FAKE", Message = line[(colon + 1)..].Trim(), Line = number, Column = 1 });
            }
            else if (line.StartsWith("missing module ", StringComparison.Ordinal))
            {
                missing = line["missing module ".Length..];
            }
        }

        return new DiagnosticParseResult(diagnostics, missing);
    }
}

public sealed class FakePackageManager : IPackageManager
{
    public ConcurrentQueue<string> Ran { get; } = new();

    public string ToolName => "fakepkg";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        if (line.StartsWith("%fakepkg ", StringComparison.Ordinal))
        {
            var args = line["%fakepkg ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            command = new PackageCommand(line, args);
            return true;
        }

        command = null!;
        return false;
    }

    public PackageCommand InstallCommand(string package) => new($"%fakepkg install {package}", ["install", package]);

    public string PackageForMissingDependency(string missingName) => "fake-" + missingName;

    public Task<PackageCommandResult> RunAsync(PackageCommand command, ToolchainInfo toolchain, Action<string> output, CancellationToken ct = default)
    {
        Ran.Enqueue(string.Join(' ', command.Arguments));
        output($"fakepkg {string.Join(' ', command.Arguments)}: done\n");
        return Task.FromResult(new PackageCommandResult(true, "Installed."));
    }
}

public sealed class FakeKernelFactory(FakePackageManager? packages = null) : INotebookKernelFactory
{
    public ConcurrentQueue<FakeKernel> Created { get; } = new();

    public INotebookKernel Create(KernelCreationContext context)
    {
        var kernel = new FakeKernel(context, packages);
        Created.Enqueue(kernel);
        return kernel;
    }
}

/// <summary>
/// Runs a tiny language: <c>name = value</c> stores a JSON value, <c>print text</c> prints, <c>show name</c> prints a
/// value's JSON, <c>ask prompt</c> reads a line of input, <c>fail message</c> fails the cell, <c>import module</c> needs
/// the package <c>fake-module</c> installed (with <see cref="FakePackageManager"/>).
/// </summary>
public sealed class FakeKernel(KernelCreationContext context, FakePackageManager? packages = null) : INotebookKernel
{
    private readonly Dictionary<string, string> _values = new();

    public KernelCreationContext Context { get; } = context;
    public int ResetCount;
    public bool Disposed { get; private set; }
    public List<string> Executed { get; } = new();

    public string LanguageId => FakeLanguage.LanguageId;
    public string DisplayName => "FakeLang 1.2.3";
    public bool IsSessionActive => _values.Count > 0 || Executed.Count > 0;
    public bool CanForceStop => true;

    public async Task<KernelExecutionResult> ExecuteAsync(KernelExecutionRequest request, CancellationToken ct)
    {
        Executed.Add(request.Code);
        var output = new System.Text.StringBuilder();
        void Print(string text)
        {
            output.Append(text);
            request.OnConsole?.Invoke(text);
        }

        foreach (var raw in request.Code.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal)) continue;
            if (line.StartsWith("print ", StringComparison.Ordinal)) Print(line[6..] + "\n");
            else if (line.StartsWith("show ", StringComparison.Ordinal))
            {
                var name = line[5..].Trim();
                if (!_values.TryGetValue(name, out var json))
                {
                    return new KernelExecutionResult { Success = false, ErrorMessage = $"{name} is not defined", MissingName = name, ConsoleOutput = output.ToString() };
                }
                Print(json + "\n");
            }
            else if (line.StartsWith("ask ", StringComparison.Ordinal))
            {
                var answer = request.OnInputRequest == null ? null : await request.OnInputRequest(line[4..], false, ct);
                Print($"answer: {answer ?? "<eof>"}\n");
            }
            else if (line.StartsWith("fail ", StringComparison.Ordinal))
            {
                return new KernelExecutionResult { Success = false, ErrorMessage = line[5..], ConsoleOutput = output.ToString() };
            }
            else if (line.StartsWith("import ", StringComparison.Ordinal))
            {
                var module = line[7..].Trim();
                if (packages == null || !packages.Ran.Contains("install fake-" + module))
                {
                    Print($"no module named {module}\n");
                    return new KernelExecutionResult { Success = false, ErrorMessage = $"no module named {module}", MissingDependency = module, ConsoleOutput = output.ToString() };
                }
            }
            else if (line.IndexOf('=') is var eq and > 0)
            {
                _values[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }

        return new KernelExecutionResult { Success = true, ConsoleOutput = output.ToString() };
    }

    public Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NotebookVariableInfo>>(_values
            .Select(kv => new NotebookVariableInfo { Name = kv.Key, TypeName = "json", ValueDisplay = kv.Value })
            .ToList());

    public Task<string> GetValueJsonAsync(string name, CancellationToken ct) =>
        _values.TryGetValue(name, out var json)
            ? Task.FromResult(json)
            : throw new KernelValueException($"FakeLang has no variable named '{name}'.");

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        _values[name] = json;
        return Task.CompletedTask;
    }

    public void HardReset()
    {
        ResetCount++;
        _values.Clear();
        Executed.Clear();
    }

    public void Dispose() => Disposed = true;
}
