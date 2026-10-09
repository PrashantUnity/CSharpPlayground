using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using Xunit;

namespace CSharpEditorPlugin.Tests.Debugging;

[Collection(ScriptDebugSessionCollection.Name)]
public class RichDebugInspectionTests
{
    [Fact]
    public void ChangeTracker_DetectsPrimitiveValueChange_AndSetsPreviousValue()
    {
        var tracker = new DebugVariableChangeTracker();

        var varX = new DebugVariableItem { Name = "count", ValueDisplay = "10", TypeName = "int" };
        var varY = new DebugVariableItem { Name = "label", ValueDisplay = "\"start\"", TypeName = "string" };

        // Step 1: First snapshot
        tracker.TrackAndMarkChanges(new[] { varX, varY });
        Assert.False(varX.HasValueChanged);
        Assert.Null(varX.PreviousValue);
        Assert.False(varY.HasValueChanged);
        Assert.Null(varY.PreviousValue);

        // Step 2: count changes to 11, label remains unchanged
        var varX2 = new DebugVariableItem { Name = "count", ValueDisplay = "11", TypeName = "int" };
        var varY2 = new DebugVariableItem { Name = "label", ValueDisplay = "\"start\"", TypeName = "string" };

        tracker.TrackAndMarkChanges(new[] { varX2, varY2 });
        Assert.True(varX2.HasValueChanged);
        Assert.Equal("10", varX2.PreviousValue);
        Assert.False(varY2.HasValueChanged);
        Assert.Null(varY2.PreviousValue);

        // Step 3: count unchanged, label changes
        var varX3 = new DebugVariableItem { Name = "count", ValueDisplay = "11", TypeName = "int" };
        var varY3 = new DebugVariableItem { Name = "label", ValueDisplay = "\"finish\"", TypeName = "string" };

        tracker.TrackAndMarkChanges(new[] { varX3, varY3 });
        Assert.False(varX3.HasValueChanged);
        Assert.True(varY3.HasValueChanged);
        Assert.Equal("\"start\"", varY3.PreviousValue);
    }

    [Fact]
    public void ChangeTracker_TracksNestedProperties_UsingPathExpression()
    {
        var tracker = new DebugVariableChangeTracker();

        var childAge1 = new DebugVariableItem
        {
            Name = "Age",
            PathExpression = "user.Age",
            ValueDisplay = "25",
            TypeName = "int"
        };
        var parent1 = new DebugVariableItem
        {
            Name = "user",
            PathExpression = "user",
            ValueDisplay = "{Person}",
            TypeName = "Person",
            Children = { childAge1 }
        };

        tracker.TrackAndMarkChanges(new[] { parent1 });
        Assert.False(childAge1.HasValueChanged);

        // Next evaluation: Age changed to 26
        var childAge2 = new DebugVariableItem
        {
            Name = "Age",
            PathExpression = "user.Age",
            ValueDisplay = "26",
            TypeName = "int"
        };
        var parent2 = new DebugVariableItem
        {
            Name = "user",
            PathExpression = "user",
            ValueDisplay = "{Person}",
            TypeName = "Person",
            Children = { childAge2 }
        };

        tracker.TrackAndMarkChanges(new[] { parent2 });
        Assert.True(childAge2.HasValueChanged);
        Assert.Equal("25", childAge2.PreviousValue);
    }

    [Fact]
    public void ChangeTracker_Reset_ClearsAllTracking()
    {
        var tracker = new DebugVariableChangeTracker();

        var item1 = new DebugVariableItem { Name = "x", ValueDisplay = "100" };
        tracker.TrackAndMarkChanges(new[] { item1 });

        tracker.Reset();

        var item2 = new DebugVariableItem { Name = "x", ValueDisplay = "200" };
        tracker.TrackAndMarkChanges(new[] { item2 });

        // Since it was reset, item2 is treated as a fresh snapshot
        Assert.False(item2.HasValueChanged);
        Assert.Null(item2.PreviousValue);
    }

    [Fact]
    public void DebugVariableItem_DetectsCollectionAndMatrixIconKinds()
    {
        var matrixItem = new DebugVariableItem
        {
            Name = "board",
            TypeName = "string[,]",
            ValueDisplay = "string[3, 3]",
            IsCollection = true
        };
        Assert.Equal("Table", matrixItem.IconKind);
        Assert.Equal("Local", matrixItem.NodeKind);

        var listItem = new DebugVariableItem
        {
            Name = "names",
            TypeName = "List<string>",
            ValueDisplay = "List<string>[5]",
            IsCollection = true
        };
        Assert.Equal("Table", listItem.IconKind);

        var objItem = new DebugVariableItem
        {
            Name = "person",
            TypeName = "Person",
            ValueDisplay = "{Person}",
            HasChildren = true
        };
        Assert.Equal("CubeOutline", objItem.IconKind);

        var jsonItem = new DebugVariableItem
        {
            Name = "payload",
            TypeName = "string",
            ValueDisplay = "{\"id\": 1, \"name\": \"test\"}"
        };
        Assert.True(jsonItem.IsTextOrStructured);
    }

    [Fact]
    public void CollectionView_Extracts1DCollectionRows()
    {
        var item = new DebugVariableItem
        {
            Name = "scores",
            TypeName = "int[]",
            ValueDisplay = "int[3]",
            IsCollection = true,
            Children =
            {
                new DebugVariableItem { Name = "[0]", ValueDisplay = "95" },
                new DebugVariableItem { Name = "[1]", ValueDisplay = "88" },
                new DebugVariableItem { Name = "[2]", ValueDisplay = "100" }
            }
        };

        var (headers, rows) = CollectionViewControl.ExtractData(item);

        Assert.Equal(new[] { "#", "Value" }, headers);
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "0", "95" }, rows[0]);
        Assert.Equal(new[] { "1", "88" }, rows[1]);
        Assert.Equal(new[] { "2", "100" }, rows[2]);
    }

    [Fact]
    public void CollectionView_Extracts2DMatrixRows()
    {
        var item = new DebugVariableItem
        {
            Name = "grid",
            TypeName = "string[,]",
            ValueDisplay = "string[2, 2]",
            IsCollection = true,
            Children =
            {
                new DebugVariableItem { Name = "[0, 0]", ValueDisplay = "\"X\"" },
                new DebugVariableItem { Name = "[0, 1]", ValueDisplay = "\"O\"" },
                new DebugVariableItem { Name = "[1, 0]", ValueDisplay = "\"O\"" },
                new DebugVariableItem { Name = "[1, 1]", ValueDisplay = "\"X\"" }
            }
        };

        var (headers, rows) = CollectionViewControl.ExtractData(item);

        Assert.Equal(new[] { "#", "0", "1" }, headers);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "0", "X", "O" }, rows[0]);
        Assert.Equal(new[] { "1", "O", "X" }, rows[1]);
    }

    [Fact]
    public void CollectionView_ExtractsObjectCollectionProperties()
    {
        var item = new DebugVariableItem
        {
            Name = "customers",
            TypeName = "List<Customer>",
            ValueDisplay = "List<Customer>[2]",
            IsCollection = true,
            Children =
            {
                new DebugVariableItem
                {
                    Name = "[0]",
                    ValueDisplay = "{Customer}",
                    Children =
                    {
                        new DebugVariableItem { Name = "Id", ValueDisplay = "1" },
                        new DebugVariableItem { Name = "Name", ValueDisplay = "\"Alice\"" }
                    }
                },
                new DebugVariableItem
                {
                    Name = "[1]",
                    ValueDisplay = "{Customer}",
                    Children =
                    {
                        new DebugVariableItem { Name = "Id", ValueDisplay = "2" },
                        new DebugVariableItem { Name = "Name", ValueDisplay = "\"Bob\"" }
                    }
                }
            }
        };

        var (headers, rows) = CollectionViewControl.ExtractData(item);

        Assert.Equal(new[] { "#", "Id", "Name" }, headers);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "0", "1", "Alice" }, rows[0]);
        Assert.Equal(new[] { "1", "2", "Bob" }, rows[1]);
    }

    [Fact]
    public async Task ScriptDebugSession_Inspects2DArray_GeneratesIndexedMatrixChildren()
    {
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();
        var debugger = new ScriptDebuggerService(compiler, engine);

        var code = @"
string[,] board = new string[,] { { ""A"", ""B"" }, { ""C"", ""D"" } };
int x = 42;
";
        var (success, bytes, diagnostics) = debugger.CompileForDebugging(code, ExecutionLanguageMode.Statements);
        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var breakpoints = new[] { new BreakpointItem { LineNumber = 3, IsEnabled = true } };
        var session = ScriptDebugSession.BeginSession(breakpoints, cts, "matrix-test");

        IReadOnlyList<DebugVariableItem>? capturedLocals = null;
        session.Paused += (line, locals) =>
        {
            capturedLocals = locals;
            session.Continue();
        };

        try
        {
            var result = await engine.ExecuteAsync(bytes!, _ => { }, cts.Token);
            Assert.True(result.Success, result.Error);
        }
        finally
        {
            ScriptDebugSession.EndSession();
        }

        Assert.NotNull(capturedLocals);
        var boardVar = capturedLocals.FirstOrDefault(v => v.Name == "board");
        Assert.NotNull(boardVar);
        Assert.True(boardVar.IsCollection);
        Assert.Equal("string[,]", boardVar.TypeName, ignoreCase: true);
        Assert.Equal(4, boardVar.CollectionItemCount);
        Assert.Equal("Table", boardVar.IconKind);

        // Verify children have [row, col] format
        var children = boardVar.Children;
        Assert.Equal(4, children.Count);
        Assert.Contains(children, c => c.Name == "[0, 0]" && c.ValueDisplay.Contains("A"));
        Assert.Contains(children, c => c.Name == "[0, 1]" && c.ValueDisplay.Contains("B"));
        Assert.Contains(children, c => c.Name == "[1, 0]" && c.ValueDisplay.Contains("C"));
        Assert.Contains(children, c => c.Name == "[1, 1]" && c.ValueDisplay.Contains("D"));
    }

    [Fact]
    public async Task ScriptDebugSession_InspectsHttpRequestAndResponse()
    {
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();
        var debugger = new ScriptDebuggerService(compiler, engine);

        var code = @"
using System.Net;
using System.Net.Http;
using System.Text;

var request = new HttpRequestMessage(HttpMethod.Post, ""https://example.com/api/users"");
request.Headers.Add(""User-Agent"", ""FryPDF/1.0"");
request.Content = new StringContent(""{ \""name\"": \""Alice\"" }"", Encoding.UTF8, ""application/json"");

var response = new HttpResponseMessage(HttpStatusCode.Created)
{
    Content = new StringContent(""{ \""id\"": 101, \""status\"": \""created\"" }"", Encoding.UTF8, ""application/json""),
    RequestMessage = request
};
response.Headers.Add(""X-RateLimit-Limit"", ""100"");

int checkpoint = 1;
";
        var (success, bytes, diagnostics) = debugger.CompileForDebugging(code, ExecutionLanguageMode.Statements);
        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var breakpoints = new[] { new BreakpointItem { LineNumber = 17, IsEnabled = true } };
        var session = ScriptDebugSession.BeginSession(breakpoints, cts, "http-test");

        IReadOnlyList<DebugVariableItem>? capturedLocals = null;
        session.Paused += (line, locals) =>
        {
            capturedLocals = locals;
            session.Continue();
        };

        try
        {
            var result = await engine.ExecuteAsync(bytes!, _ => { }, cts.Token);
            Assert.True(result.Success, result.Error);
        }
        finally
        {
            ScriptDebugSession.EndSession();
        }

        Assert.NotNull(capturedLocals);
        var reqVar = capturedLocals.FirstOrDefault(v => v.Name == "request");
        var respVar = capturedLocals.FirstOrDefault(v => v.Name == "response");

        Assert.NotNull(reqVar);
        Assert.NotNull(respVar);

        // 1. Verify single-line clean summaries
        Assert.Contains("POST", reqVar.ValueDisplay);
        Assert.Contains("https://example.com/api/users", reqVar.ValueDisplay);
        Assert.Equal("Web", reqVar.IconKind);

        Assert.Contains("201", respVar.ValueDisplay);
        Assert.Contains("Created", respVar.ValueDisplay);
        Assert.Equal("Web", respVar.IconKind);

        // 2. Verify Request extracted body and flattened headers
        var reqBody = reqVar.Children.FirstOrDefault(c => c.Name == "[Body]");
        Assert.NotNull(reqBody);
        Assert.Contains("Alice", reqBody.ValueDisplay);
        Assert.True(reqBody.IsTextOrStructured);

        var reqHeaders = reqVar.Children.FirstOrDefault(c => c.Name == "Headers");
        Assert.NotNull(reqHeaders);
        var uaHeader = reqHeaders.Children.FirstOrDefault(c => c.Name == "User-Agent");
        Assert.NotNull(uaHeader);
        Assert.Contains("FryPDF/1.0", uaHeader.ValueDisplay);

        // 3. Verify Response extracted body and flattened headers
        var respBody = respVar.Children.FirstOrDefault(c => c.Name == "[Body]");
        Assert.NotNull(respBody);
        Assert.Contains("101", respBody.ValueDisplay);
        Assert.Contains("created", respBody.ValueDisplay);
        Assert.True(respBody.IsTextOrStructured);

        var respHeaders = respVar.Children.FirstOrDefault(c => c.Name == "Headers");
        Assert.NotNull(respHeaders);
        var rateLimitHeader = respHeaders.Children.FirstOrDefault(c => c.Name == "X-RateLimit-Limit");
        Assert.NotNull(rateLimitHeader);
        Assert.Contains("100", rateLimitHeader.ValueDisplay);

        // 4. Verify .Dump() produces a rich HTTP table
        var responseTable = DumpTableBuilder.Create(respVar.RawValue);
        Assert.NotNull(responseTable);
        Assert.Contains("201 Created", responseTable.Title);
        Assert.Contains(responseTable.Rows, r => r.Cells[0].DisplayText == "Status" && r.Cells[1].DisplayText.Contains("201 Created"));
        Assert.Contains(responseTable.Rows, r => r.Cells[0].DisplayText == "Body Payload" && r.Cells[1].DisplayText.Contains("created"));
    }

    [Fact]
    public void HttpInspectionHelper_ParsesServerSentEvents_Correctly()
    {
        var rawSse = "event: message\ndata: {\"greeting\": \"hello\", \"count\": 1}\n\nevent: update\ndata: {\"greeting\": \"world\", \"count\": 2}\nid: 42\n\n";
        var events = HttpInspectionHelper.ParseSseEvents(rawSse);

        Assert.Equal(2, events.Count);

        Assert.Equal("message", events[0]["event"]);
        Assert.Contains("hello", events[0]["data"]);

        Assert.Equal("update", events[1]["event"]);
        Assert.Contains("world", events[1]["data"]);
        Assert.Equal("42", events[1]["id"]);
    }
}
