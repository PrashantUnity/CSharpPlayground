using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// C# notebooks whose cells compile at the same time. Before cells compiled one at a time, about a third of the
/// notebooks that compiled their first cells together in a fresh process got "'List&lt;double&gt;' does not contain a
/// definition for 'Sum'" from a later cell using an earlier cell's list (Roslyn gave their submissions .NET types of
/// their own). Run on its own, this test is the first thing the process compiles, which is when it happened.
/// </summary>
public class NotebookKernelConcurrencyTests
{
    private static async Task<string?> Notebook()
    {
        INotebookKernel kernel = new NotebookExecutionKernel();
        foreach (var cell in new[] { "List<double> values = new() { 0.5, 1.5 };", "var words = new Dictionary<string, int> { [\"a\"] = 1 };", "values.Sum() + words.Values.Sum()" })
        {
            var output = new ConcurrentQueue<string>();
            var result = await kernel.ExecuteAsync(new KernelExecutionRequest { Code = cell, OnConsole = output.Enqueue }, CancellationToken.None);
            if (!result.Success) return $"{cell}: {result.ErrorMessage}";
            if (cell.StartsWith("values.Sum", StringComparison.Ordinal) && string.Concat(output).Trim() != "3") return $"{cell} gave {string.Concat(output)}";
        }

        return null;
    }

    [Fact]
    public async Task ManyNotebooksCompilingAtOnce_CanAllUseWhatTheirEarlierCellsMade()
    {
        var failures = (await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Task.Run(Notebook)))).Where(f => f != null).ToList();

        Assert.Empty(failures);
    }
}
