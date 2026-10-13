using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// Runs what a cell's directives ask for before its code: package commands (<c>#!pip install</c>, <c>#!npm install</c>…)
/// and <c>#!share --from</c> copies of values from another language's kernel. Every host runs a notebook's cells through
/// it, so a directive behaves and fails the same way in each, with the same messages.
/// </summary>
public sealed class NotebookDirectiveRunner(ILanguageCatalog languages, NotebookKernelRouter router)
{
    /// <summary>True when <paramref name="language"/> can run notebook cells.</summary>
    public static bool IsNotebookLanguage(ILanguageEngine language) =>
        language.NotebookKernels != null && language.Has(LanguageCapabilities.NotebookCells);

    /// <summary>Runs the directives in order; false (with the reason written) at the first that fails.</summary>
    public async Task<bool> RunAsync(
        CellDirectives directives,
        ILanguageEngine language,
        INotebookKernel kernel,
        ToolchainQuery query,
        Action<string> write,
        CancellationToken ct)
    {
        foreach (var command in directives.PackageCommands)
        {
            if (!await RunPackageCommandAsync(language, command, kernel, query, write, ct)) return false;
        }

        foreach (var share in directives.Shares)
        {
            if (!await ShareAsync(share, language, kernel, write, ct)) return false;
        }

        return true;
    }

    /// <summary>Runs one package command (an install the user typed, or one for a missing dependency).</summary>
    public async Task<bool> RunPackageCommandAsync(
        ILanguageEngine language,
        PackageCommand command,
        INotebookKernel kernel,
        ToolchainQuery query,
        Action<string> write,
        CancellationToken ct)
    {
        if (language.Packages is not { } packages || language.Toolchain is not { } toolchains)
        {
            write($"❌ {language.DisplayName} cells can't install packages.\n");
            return false;
        }

        var resolution = await toolchains.ResolveAsync(query, ct);
        if (resolution.Toolchain is not { } toolchain)
        {
            write((resolution.Missing?.ToText() ?? $"{toolchains.ToolName} wasn't found.") + "\n");
            return false;
        }

        write($"▶ {command.Text}\n");
        var result = await packages.RunAsync(command, toolchain, write, ct);
        if (result.Message is { Length: > 0 } message) write((result.Success ? "" : "❌ ") + message.TrimEnd() + "\n");

        // A new environment only takes over when the kernel next starts; until then the running one looks there too.
        if (result.Success && result.AddedSearchPath is { } searchPath && kernel is ISearchPathKernel searchable && kernel.IsSessionActive)
        {
            await searchable.AddSearchPathAsync(searchPath, ct);
        }

        return result.Success;
    }

    private async Task<bool> ShareAsync(ShareDirective share, ILanguageEngine language, INotebookKernel kernel, Action<string> write, CancellationToken ct)
    {
        var from = languages.FindLanguage(share.FromLanguage);
        string? problem = null;
        if (from == null || !IsNotebookLanguage(from))
        {
            problem = $"There's no notebook language called \"{share.FromLanguage}\". Use {string.Join(" or ", languages.NotebookCellLanguages.Select(l => l.Id))}.";
        }
        else if (!from.Has(LanguageCapabilities.ValueSharing) || !language.Has(LanguageCapabilities.ValueSharing))
        {
            problem = $"{(from.Has(LanguageCapabilities.ValueSharing) ? language.DisplayName : from.DisplayName)} cells can't share values.";
        }
        else if (string.Equals(from.Id, language.Id, StringComparison.OrdinalIgnoreCase))
        {
            problem = $"'{share.Name}' is already a {language.DisplayName} value: #!share copies from another language's cells.";
        }
        else if (router.Find(from.Id) is not { IsSessionActive: true } source)
        {
            problem = $"No {from.DisplayName} cell has run yet, so there's no '{share.Name}' to copy. Run the {from.DisplayName} cell that sets it first.";
        }
        else
        {
            try
            {
                var json = await source.GetValueJsonAsync(share.Name, ct);
                await kernel.SetValueFromJsonAsync(share.TargetName, json, ct);
                return true;
            }
            catch (KernelValueException ex)
            {
                problem = ex.Message;
            }
        }

        write($"❌ #!share (line {share.LineNumber}): {problem}\n");
        return false;
    }
}
