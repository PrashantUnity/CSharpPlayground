using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

/// <summary>
/// The hover card <see cref="CSharpQuickInfoController"/> shows: the symbol's signature and container in the editor's
/// monospace font and syntax colours, then its summary, parameters, return value, exceptions and remarks.
/// </summary>
public partial class QuickInfoTipControl : UserControl
{
    private static readonly FontFamily MonoFont = new("JetBrains Mono, Menlo, Monaco, Consolas, monospace");

    // The editor's own syntax colours (CSharpSyntaxHighlightingTheme: VS Code Dark+ and Light+).
    private static readonly Dictionary<QuickInfoTextKind, IBrush> DarkPalette = Palette(
        keyword: "#569CD6", type: "#4EC9B0", valueType: "#86C691", interfaceType: "#B8D7A3", method: "#DCDCAA",
        member: "#9CDCFE", constant: "#4FC1FF", text: "#CE9178", number: "#B5CEA8", code: "#D7BA7D", link: "#4FC1FF");

    private static readonly Dictionary<QuickInfoTextKind, IBrush> LightPalette = Palette(
        keyword: "#0000FF", type: "#267F99", valueType: "#267F99", interfaceType: "#267F99", method: "#795E26",
        member: "#001080", constant: "#0070C1", text: "#A31515", number: "#098658", code: "#A31515", link: "#2563EB");

    private static readonly IBrush DarkLabel = new ImmutableSolidColorBrush(Color.Parse("#9BA1AD"));
    private static readonly IBrush LightLabel = new ImmutableSolidColorBrush(Color.Parse("#44474E"));

    public QuickInfoTipControl()
    {
        InitializeComponent();
    }

    /// <summary>Shows <paramref name="info"/>, coloured for the editor's dark or light theme.</summary>
    public void SetQuickInfo(CSharpQuickInfo info, bool isDark)
    {
        Fill(SignatureText, info.Signature, isDark);

        var details = new List<QuickInfoTextRun>(info.Container);
        foreach (var line in info.TypeArguments)
        {
            if (details.Count > 0) details.Add(new QuickInfoTextRun("\n", QuickInfoTextKind.LineBreak));
            details.AddRange(line);
        }
        Fill(DetailsText, details, isDark);
        DetailsText.IsVisible = details.Count > 0;

        var documentation = info.Documentation;
        DocumentationPanel.Children.Clear();
        AddParagraph(null, documentation.Summary, isDark);
        AddList("Type parameters:", documentation.TypeParameters, isDark);
        AddList("Parameters:", documentation.Parameters, isDark);
        AddParagraph("Returns:", documentation.Returns, isDark);
        AddParagraph("Value:", documentation.Value, isDark);
        AddList("Exceptions:", documentation.Exceptions, isDark);
        AddParagraph("Remarks:", documentation.Remarks, isDark);

        var hasDocumentation = DocumentationPanel.Children.Count > 0;
        DocumentationPanel.IsVisible = hasDocumentation;
        DocumentationSeparator.IsVisible = hasDocumentation;
    }

    private void AddParagraph(string? label, IReadOnlyList<QuickInfoTextRun> text, bool isDark)
    {
        if (text.Count == 0) return;

        var runs = new List<QuickInfoTextRun>();
        if (label != null) runs.Add(new QuickInfoTextRun(label + " ", QuickInfoTextKind.Label));
        runs.AddRange(text);
        AddBlock(runs, isDark);
    }

    // "Parameters:" then one "name – description" line each; undocumented names are left out.
    private void AddList(string label, IReadOnlyList<QuickInfoNamedSection> sections, bool isDark)
    {
        var documented = sections.Where(s => s.Text.Count > 0).ToList();
        if (documented.Count == 0) return;

        var runs = new List<QuickInfoTextRun> { new(label, QuickInfoTextKind.Label) };
        foreach (var section in documented)
        {
            runs.Add(new QuickInfoTextRun("\n", QuickInfoTextKind.LineBreak));
            runs.Add(section.Name);
            runs.Add(new QuickInfoTextRun(" – ", QuickInfoTextKind.Label));
            runs.AddRange(section.Text);
        }
        AddBlock(runs, isDark);
    }

    private void AddBlock(IReadOnlyList<QuickInfoTextRun> runs, bool isDark)
    {
        var block = new SelectableTextBlock { FontSize = 12.5, LineHeight = 18, TextWrapping = TextWrapping.Wrap };
        Fill(block, runs, isDark);
        DocumentationPanel.Children.Add(block);
    }

    private void Fill(TextBlock block, IEnumerable<QuickInfoTextRun> runs, bool isDark)
    {
        var inlines = block.Inlines ??= new InlineCollection();
        inlines.Clear();
        foreach (var run in runs)
        {
            if (run.Kind == QuickInfoTextKind.LineBreak)
            {
                foreach (var _ in run.Text) inlines.Add(new LineBreak());
                continue;
            }

            var lines = run.Kind == QuickInfoTextKind.CodeBlock ? run.Text.Split('\n') : [run.Text];
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0) inlines.Add(new LineBreak());
                inlines.Add(CreateRun(lines[i], run.Kind, isDark));
            }
        }
    }

    private Run CreateRun(string text, QuickInfoTextKind kind, bool isDark)
    {
        var run = new Run(text);
        if (kind == QuickInfoTextKind.Label)
        {
            run.Foreground = LabelBrush(isDark);
        }
        else if ((isDark ? DarkPalette : LightPalette).TryGetValue(kind, out var brush))
        {
            run.Foreground = brush;
        }

        // Code and symbol names read as code inside the documentation's proportional text.
        if (kind is not (QuickInfoTextKind.Text or QuickInfoTextKind.Label or QuickInfoTextKind.Link))
        {
            run.FontFamily = MonoFont;
        }
        return run;
    }

    private IBrush LabelBrush(bool isDark) =>
        this.TryFindResource("M3OnSurfaceVariantBrush", isDark ? ThemeVariant.Dark : ThemeVariant.Light, out var value) && value is IBrush brush
            ? brush
            : isDark ? DarkLabel : LightLabel;

    private static Dictionary<QuickInfoTextKind, IBrush> Palette(
        string keyword, string type, string valueType, string interfaceType, string method, string member,
        string constant, string text, string number, string code, string link)
    {
        IBrush Brush(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));
        return new Dictionary<QuickInfoTextKind, IBrush>
        {
            [QuickInfoTextKind.Keyword] = Brush(keyword),
            [QuickInfoTextKind.Type] = Brush(type),
            [QuickInfoTextKind.Struct] = Brush(valueType),
            [QuickInfoTextKind.Interface] = Brush(interfaceType),
            [QuickInfoTextKind.TypeParameter] = Brush(interfaceType),
            [QuickInfoTextKind.Method] = Brush(method),
            [QuickInfoTextKind.Member] = Brush(member),
            [QuickInfoTextKind.Variable] = Brush(member),
            [QuickInfoTextKind.Constant] = Brush(constant),
            [QuickInfoTextKind.StringLiteral] = Brush(text),
            [QuickInfoTextKind.NumericLiteral] = Brush(number),
            [QuickInfoTextKind.Code] = Brush(code),
            [QuickInfoTextKind.CodeBlock] = Brush(code),
            [QuickInfoTextKind.Link] = Brush(link)
        };
    }
}
