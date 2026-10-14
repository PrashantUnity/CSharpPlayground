using System.Runtime.InteropServices;
using FrySharp.Sidecar.Hosting;
using FrySharp.Sidecar.Protocol;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace FrySharp.Sidecar.Services;

/// <summary>What the engine is running on and what each language found to run with, for "FrySharp: Show Engine Info".</summary>
public sealed class EngineInfoService(LanguageRegistry<ILanguageEngine> languages, WorkspaceService workspace)
{
    // A probe runs the tool (python --version ...): none may hold the answer up for long.
    private static readonly TimeSpan ProbeLimit = TimeSpan.FromSeconds(20);

    public async Task<EngineInfo> GetAsync(CancellationToken ct)
    {
        var reports = await Task.WhenAll(languages.All
            .Where(language => NotebookDirectiveRunner.IsNotebookLanguage(language) || FileRunner.CanRun(language))
            .Select(language => ReportAsync(language, ct)));

        return new EngineInfo(
            typeof(EngineInfoService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
            ProtocolVersion.Current,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            workspace.Paths.Root,
            reports.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static async Task<ToolchainReport> ReportAsync(ILanguageEngine language, CancellationToken ct)
    {
        // C# runs inside the engine itself: its toolchain is the runtime the engine runs on
        if (language.Toolchain == null)
        {
            return new ToolchainReport(language.Id, language.DisplayName, true, RuntimeInformation.FrameworkDescription, Environment.Version.ToString(3), null, "built into the engine", null);
        }

        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(ProbeLimit);
            var resolution = await language.Toolchain.ResolveAsync(new ToolchainQuery(), limit.Token).ConfigureAwait(false);
            if (resolution.Toolchain is { } found)
            {
                return new ToolchainReport(language.Id, language.DisplayName, true, found.DisplayName, found.Version.ToString(), found.ExecutablePath, found.Source, null);
            }

            return new ToolchainReport(language.Id, language.DisplayName, false, null, null, null, null, resolution.Missing?.ToText() ?? $"{language.DisplayName}'s tools were not found.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ToolchainReport(language.Id, language.DisplayName, false, null, null, null, null, "Looking for it took too long.");
        }
    }
}
