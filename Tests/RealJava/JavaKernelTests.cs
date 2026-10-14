using System.Collections.Concurrent;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealJava;

/// <summary>The notebook's Java kernel with real JDK: state, output, errors, values, variables.</summary>
[Collection(RealJavaCollection.Name)]
public class JavaKernelTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_JavaKernel_" + Guid.NewGuid().ToString("N"));
    private readonly string _notebookFolder;
    private readonly List<INotebookKernel> _kernels = new();

    public JavaKernelTests()
    {
        _notebookFolder = Path.Combine(_baseDir, "notebooks");
        Directory.CreateDirectory(_notebookFolder);
    }

    public void Dispose()
    {
        foreach (var kernel in _kernels) kernel.Dispose();
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private ProtocolKernel Kernel()
    {
        var services = new StudioLanguageServices(Path.Combine(_baseDir, "services"));
        var java = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;
        java.JavaToolchain.Select(TestJava.Require().ExecutablePath);
        var kernel = (ProtocolKernel)java.Engine.NotebookKernels.Create(new KernelCreationContext(() => _notebookFolder));
        _kernels.Add(kernel);
        return kernel;
    }

    private sealed record Run(KernelExecutionResult Result, string Console, List<RichCellOutput> Rich, List<string> Chunks);

    private static async Task<Run> Execute(ProtocolKernel kernel, string code, CancellationToken ct = default, Func<string, bool, CancellationToken, Task<string?>>? ask = null)
    {
        var chunks = new ConcurrentQueue<string>();
        var rich = new ConcurrentQueue<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code.Replace("\r\n", "\n"),
            SourceId = "cell",
            OnConsole = chunks.Enqueue,
            OnRichOutput = rich.Enqueue,
            OnInputRequest = ask
        }, ct).WaitAsync(Patience);
        return new Run(result, string.Concat(chunks), rich.ToList(), chunks.ToList());
    }

    [JavaFact]
    public async Task State_CarriesOverBetweenCells_AndTheLastExpressionIsShown()
    {
        var kernel = Kernel();

        var first = await Execute(kernel, "int x = 41;");
        var second = await Execute(kernel, "x + 1;");

        Assert.True(first.Result.Success, first.Console);
        Assert.Equal("42\n", second.Console);
        Assert.StartsWith("Java", kernel.DisplayName);
    }

    [JavaFact]
    public async Task LastExpression_AnObject_OpensInTheObjectExplorer_LikeACSharpValue()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, """
            public class People {
                public String name = "";
                public String className = "";
                public int[] numbers = {1, 34, 45, 235, 25};
                public People another;
            }

            People person = new People();
            person.name = "Prashant";
            person.another = person;
            person
            """);

        Assert.True(run.Result.Success, run.Console);
        Assert.DoesNotContain("People@", run.Console);
        var node = Assert.IsType<ObjectInspectorNode>(Assert.Single(run.Rich, r => r.Kind == CellOutputKind.ObjectInspector).InspectorNode);
        Assert.Equal("People", node.HeaderTitle);
        Assert.Equal(["name", "className", "numbers", "another"], node.Properties.Select(p => p.Name));
        Assert.Equal("Prashant", node.Properties[0].SimpleValueText);
        Assert.True(node.Properties[0].IsString);
        Assert.Equal("[ 1, 34, 45, 235, 25 ]", node.Properties[2].SimpleValueText);
        Assert.True(node.Properties[3].ChildNode!.IsCircularReference);
        InspectorChain.AssertFiveLevels(node, "another", ["name", "className", "numbers", "another"]);
    }

    [JavaFact]
    public async Task LastExpression_RecordsAndGetters_AreInspected_ACollectionOfRecordsIsATable()
    {
        var kernel = Kernel();

        var record = await Execute(kernel, "record Point(int x, int y) {}\nnew Point(3, 4)");
        var points = await Execute(kernel, "List.of(new Point(1, 2), new Point(3, 4))");
        var bean = await Execute(kernel, "public class Person { private String name = \"Ada\"; public String getName() { return name; } public boolean isAdmin() { return true; } }\nnew Person()");

        var one = Assert.Single(record.Rich).InspectorNode!;
        Assert.Equal("Point", one.HeaderTitle);
        Assert.Equal(["x", "y"], one.Properties.Select(p => p.Name));

        // a collection of records is the studio's table (a column for each component), as in Python and F#
        var table = Assert.Single(points.Rich).TableResult!;
        Assert.Equal(["x", "y"], table.Columns.Select(c => c.Header));
        Assert.Equal([true, true], table.Columns.Select(c => c.IsNumeric));
        Assert.Equal(["1", "3"], table.Rows.Select(r => r.Cells[0].DisplayText));
        Assert.Contains("Point", table.Title);

        // a collection of collections has no columns: it opens as a tree
        var nested = Assert.Single((await Execute(kernel, "List.of(List.of(new Point(1, 2)), List.of(new Point(3, 4)))")).Rich).InspectorNode!;
        Assert.Equal("List", nested.HeaderTitle); // not the JDK's hidden List12
        Assert.Equal(["[0]", "[1]"], nested.Properties.Select(p => p.Name));

        var person = Assert.Single(bean.Rich).InspectorNode!;
        Assert.Equal(["admin", "name"], person.Properties.Select(p => p.Name));
        Assert.Equal("Ada", person.Properties.Single(p => p.Name == "name").SimpleValueText);
    }

    [JavaFact]
    public async Task LastExpression_AScalar_OrAShortListOfScalars_StaysText()
    {
        var kernel = Kernel();

        var text = await Execute(kernel, "\"hello\"");
        var numbers = await Execute(kernel, "new int[]{1, 2, 3}");
        var names = await Execute(kernel, "List.of(\"a\", \"b\")");

        Assert.Empty(text.Rich);
        Assert.Equal("\"hello\"\n", text.Console);
        Assert.Empty(numbers.Rich);
        Assert.Equal("[ 1, 2, 3 ]\n", numbers.Console);
        Assert.Empty(names.Rich);
        Assert.Equal("[ a, b ]\n", names.Console);
    }

    [JavaFact]
    public async Task Input_ScannerAndBufferedReader_AskTheNotebookForALine()
    {
        var kernel = Kernel();
        var answers = new Queue<string>(["Ada", "Grace"]);
        var asked = 0;
        Task<string?> Ask(string prompt, bool password, CancellationToken ct)
        {
            asked++;
            return Task.FromResult<string?>(answers.Dequeue());
        }

        var run = await Execute(kernel, """
            var scanner = new java.util.Scanner(System.in);
            System.out.print("Name? ");
            String name = scanner.nextLine();
            var reader = new java.io.BufferedReader(new java.io.InputStreamReader(System.in));
            String other = reader.readLine();
            System.out.println("Hello " + name + " and " + other);
            """, ask: Ask);

        Assert.True(run.Result.Success, run.Console + " Err: " + run.Result.ErrorMessage);
        Assert.Equal(2, asked);
        Assert.Contains("Hello Ada and Grace", run.Console);
    }

    [JavaFact]
    public async Task Input_WithNoOneToAsk_IsTheEndOfInput_NotAHang()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "System.in.read()");

        Assert.True(run.Result.Success, run.Console);
        Assert.EndsWith("-1\n", run.Console); // (the studio echoes the empty answer first, as for every language)
    }

    [JavaFact]
    public async Task OutputStreams_SystemOutPrints()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "System.out.println(\"Streamed from Java\");");

        Assert.True(run.Result.Success, run.Console + " Err: " + run.Result.ErrorMessage);
        Assert.Equal("Streamed from Java\n", run.Console.Replace("\r\n", "\n"));
    }

    [JavaFact]
    public async Task Values_CanBeShared_BothWays()
    {
        var kernel = Kernel();

        await kernel.SetValueFromJsonAsync("scores", "[88,95,72,91,84]", CancellationToken.None);
        var json = await kernel.GetValueJsonAsync("scores", CancellationToken.None);

        Assert.Equal("[88,95,72,91,84]", json);

        var run = await Execute(kernel, "scores[1];");
        Assert.Contains("95", run.Console);
    }

    [JavaFact]
    public async Task Variables_AreReported()
    {
        var kernel = Kernel();

        await Execute(kernel, "int age = 30;\nString city = \"Zurich\";");
        var vars = await kernel.GetVariablesAsync(CancellationToken.None);

        Assert.Contains(vars, v => v.Name == "age" && v.TypeName == "int" && v.ValueDisplay == "30");
        Assert.Contains(vars, v => v.Name == "city" && v.TypeName == "String");
    }
}
