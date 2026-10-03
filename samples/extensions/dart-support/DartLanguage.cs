#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace DartSupportExtension;

/// <summary>
/// Full-featured language definition for Dart, bringing Dart into interactive polyglot notebooks,
/// the VS Code script studio, toolchain manager, Problems deck, and Packages tab.
/// </summary>
public sealed class DartLanguage : LanguageDefinition
{
    private readonly DartToolchainProvider _toolchain;
    private readonly DartScriptRunner _runner;
    private readonly DartDiagnosticParser _diagnostics;
    private readonly DartPackageManager _packages;
    private readonly INotebookKernelFactory _kernels;

    public override string Id => "dart";
    public override string DisplayName => "Dart";
    public override string ShortName => "DART";
    public override IReadOnlyList<string> FileExtensions => [".dart"];
    public override IReadOnlyList<string> Aliases => ["dart"];
    public override string AccentHex => "#0175C2";
    public override string IconKind => "CodeBraces";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "Dart SDK (JIT / AOT)";
    public override string NewFileTemplate => """
        // Dart 3 Script
        void main() {
          print('Hello from FrySharp Dart!');
        }

        """;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.NotebookCells |
        LanguageCapabilities.StandardInput |
        LanguageCapabilities.LiveDiagnostics |
        LanguageCapabilities.Packages |
        LanguageCapabilities.Templates |
        LanguageCapabilities.ValueSharing;

    public override IToolchainProvider? Toolchain => _toolchain;
    public override IScriptRunner? ScriptRunner => _runner;
    public override IDiagnosticParser? RunDiagnostics => _diagnostics;
    public override IPackageManager? Packages => _packages;
    public override INotebookKernelFactory? NotebookKernels => _kernels;

    public DartLanguage(IExtensionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IHostEnvironment host = new HostEnvironment();
        IProcessLauncher launcher = new ProcessLauncher();
        ToolchainSettingsStore settings;

        if (context.App is PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext studioApp)
        {
            host = studioApp.LanguageServices.Host;
            launcher = studioApp.LanguageServices.Processes;
            settings = studioApp.LanguageServices.ToolchainSettings;
        }
        else
        {
            var settingsPath = Path.Combine(host.HomeDirectory, ".frysharp", "toolchains.json");
            settings = new ToolchainSettingsStore(settingsPath);
        }

        _toolchain = new DartToolchainProvider(host, launcher, settings);
        _runner = new DartScriptRunner(_toolchain);
        _diagnostics = new DartDiagnosticParser();
        _packages = new DartPackageManager(_toolchain, launcher, host);
        _kernels = new DelegateKernelFactory(kernelContext =>
            new DartNotebookKernel(_toolchain, launcher, host, kernelContext));
    }

    public override IHighlightingDefinition? GetHighlighting(bool isDark) =>
        DartSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) =>
        new BraceIndentationStrategy(options);
}
