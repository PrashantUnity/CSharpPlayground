using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class EditDistanceTemplateTests
{
    [Fact]
    public void EditDistanceTemplate_IsRegisteredInCodeTemplateLibrary()
    {
        var templates = CodeTemplateLibrary.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "leetcode_72_edit_distance");

        Assert.NotNull(template);
        Assert.Equal("72. Edit Distance (Interactive Multi-Approach Notebook)", template.Title);
        Assert.Equal("Algorithms", template.Category);
        Assert.Equal(WorkspaceItemKind.Notebook, template.Kind);
        Assert.True(template.IsNotebook);
        Assert.Contains("Dynamic Programming", template.Tags);
        Assert.Contains("Notebook", template.Tags);
        Assert.Contains("Visualizer", template.Tags);
        Assert.NotEmpty(template.Notes);
        Assert.NotEmpty(template.Cells);
        Assert.Equal(10, template.Cells.Count);
    }

    [Fact]
    public async Task EditDistanceTemplate_ExecutesAllApproachesAndEmitsVisualizers()
    {
        var template = CodeTemplateLibrary.GetTemplates().Single(t => t.Id == "leetcode_72_edit_distance");
        var kernel = new NotebookExecutionKernel();

        var codeCells = template.Cells.Where(c => c.Type == CellType.Code).ToList();
        Assert.Equal(4, codeCells.Count); // 4 approaches: Naive, Memo, 2D Tabulation, Rolling Rows

        // Approach 1: Naive (Emits Tree visualizer)
        var out1 = new List<RichCellOutput>();
        var res1 = await kernel.ExecuteCellAsync(codeCells[0].Source, onRichOutput: out1.Add);
        Assert.True(res1.Success, res1.ErrorMessage);
        var spec1 = out1.Select(o => o.Visual?.Spec).OfType<VisualizerSpec>().FirstOrDefault();
        Assert.NotNull(spec1);
        Assert.Equal(VisualizerKind.Tree, spec1.Kind);

        // Approach 2: Memoized (Emits Tree visualizer with memo pruning)
        var out2 = new List<RichCellOutput>();
        var res2 = await kernel.ExecuteCellAsync(codeCells[1].Source, onRichOutput: out2.Add);
        Assert.True(res2.Success, res2.ErrorMessage);
        var spec2 = out2.Select(o => o.Visual?.Spec).OfType<VisualizerSpec>().FirstOrDefault();
        Assert.NotNull(spec2);
        Assert.Equal(VisualizerKind.Tree, spec2.Kind);

        // Approach 3: 2D Tabulation DP (Emits Matrix visualizer with 18 steps and path)
        var out3 = new List<RichCellOutput>();
        var res3 = await kernel.ExecuteCellAsync(codeCells[2].Source, onRichOutput: out3.Add);
        Assert.True(res3.Success, res3.ErrorMessage);
        var spec3 = out3.Select(o => o.Visual?.Spec).OfType<VisualizerSpec>().FirstOrDefault();
        Assert.NotNull(spec3);
        Assert.Equal(VisualizerKind.Matrix, spec3.Kind);
        Assert.Equal(18, spec3.Steps.Count);

        // Approach 4: Space-Optimized Rolling Rows DP (Emits 2-row Matrix visualizer)
        var out4 = new List<RichCellOutput>();
        var res4 = await kernel.ExecuteCellAsync(codeCells[3].Source, onRichOutput: out4.Add);
        Assert.True(res4.Success, res4.ErrorMessage);
        var spec4 = out4.Select(o => o.Visual?.Spec).OfType<VisualizerSpec>().FirstOrDefault();
        Assert.NotNull(spec4);
        Assert.Equal(VisualizerKind.Matrix, spec4.Kind);
    }

    [Fact]
    public void AllTemplateIds_AreGloballyUnique()
    {
        var templates = CodeTemplateLibrary.GetTemplates();
        var duplicates = templates.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void MarkdownView_RendersSummaryTableCorrectly()
    {
        var template = CodeTemplateLibrary.GetTemplates().Single(t => t.Id == "leetcode_72_edit_distance");
        var summaryCell = template.Cells.Last();
        Assert.Equal(CellType.Markdown, summaryCell.Type);
        Assert.DoesNotContain(@"\mathcal", summaryCell.Source);

        var markdownView = new PdfEditorApp.Plugins.CSharpEditor.Controls.MarkdownView
        {
            Markdown = summaryCell.Source
        };

        var panel = Assert.IsType<Avalonia.Controls.StackPanel>(markdownView.Content);
        // Should contain Header, Rule (if any), Table (Border with Grid), Subheading, and Bullet List
        var tableBorder = panel.Children.OfType<Avalonia.Controls.Border>()
            .FirstOrDefault(b => b.Child is Avalonia.Controls.Grid);
        Assert.NotNull(tableBorder);

        var grid = Assert.IsType<Avalonia.Controls.Grid>(tableBorder.Child);
        Assert.Equal(4, grid.ColumnDefinitions.Count); // 4 columns
        Assert.Equal(5, grid.RowDefinitions.Count);    // 1 header + 4 data rows
    }
}
