using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;
using Xunit;

namespace CSharpEditorPlugin.Tests.Server;

public class FryServerExporterTests
{
    [Fact]
    public void OpenApiExporter_GeneratesValidOpenApi3Json()
    {
        var doc = new FryServerDocumentItem
        {
            Title = "Users API",
            Description = "User management test api",
            ServerConfig = new FryServerConfiguration
            {
                Host = "localhost",
                Port = 5000,
                ApiPrefix = "/api"
            },
            Cells = new List<FryServerCellItem>
            {
                new()
                {
                    Id = "get-user",
                    Method = "GET",
                    Route = "/users/{id}",
                    Title = "Get User Profile",
                    Type = FryServerCellType.Endpoint,
                    TestHarness = new FryServerTestHarnessItem
                    {
                        QueryParams = new() { ["includeOrders"] = "true" }
                    }
                }
            }
        };

        var json = FryServerOpenApiExporter.GenerateOpenApiJson(doc);

        Assert.NotNull(json);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("3.0.1", parsed.RootElement.GetProperty("openapi").GetString());
        Assert.Equal("Users API", parsed.RootElement.GetProperty("info").GetProperty("title").GetString());

        var paths = parsed.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/users/{id}", out var pathItem));
        Assert.True(pathItem.TryGetProperty("get", out var getOp));
        Assert.Equal("Get User Profile", getOp.GetProperty("summary").GetString());
    }

    [Fact]
    public void AspNetCoreExporter_GeneratesExecutableProgramCs()
    {
        var doc = new FryServerDocumentItem
        {
            ServerConfig = new FryServerConfiguration
            {
                Host = "localhost",
                Port = 5000,
                ApiPrefix = "/api",
                EnableCors = true
            },
            Cells = new List<FryServerCellItem>
            {
                new()
                {
                    Method = "GET",
                    Route = "/health",
                    Title = "Health Check",
                    Type = FryServerCellType.Endpoint,
                    Source = "return Results.Ok(new { status = \"healthy\" });"
                }
            }
        };

        var programCs = FryServerAspNetCoreExporter.ExportToMinimalApiProgramCs(doc);

        Assert.Contains("WebApplication.CreateBuilder", programCs);
        Assert.Contains("app.UseCors();", programCs);
        Assert.Contains("app.MapGet(\"/api/health\"", programCs);
        Assert.Contains("app.Run(\"http://localhost:5000\");", programCs);
    }

    [Fact]
    public void CurlGenerator_BuildsAccurateCurlString()
    {
        var config = new FryServerConfiguration
        {
            Host = "localhost",
            Port = 5000,
            ApiPrefix = "/api"
        };

        var cell = new FryServerCellItem
        {
            Method = "POST",
            Route = "/users/{id}",
            Type = FryServerCellType.Endpoint,
            TestHarness = new FryServerTestHarnessItem
            {
                PathParams = new() { ["id"] = "42" },
                QueryParams = new() { ["notify"] = "true" },
                Headers = new() { ["Authorization"] = "Bearer token123" },
                Body = "{\"name\":\"John\"}",
                BodyContentType = "application/json"
            }
        };

        var curl = FryServerCurlGenerator.GenerateCurlCommand(config, cell);

        Assert.StartsWith("curl -X POST \"http://localhost:5000/api/users/42?notify=true\"", curl);
        Assert.Contains("-H \"Authorization: Bearer token123\"", curl);
        Assert.Contains("-H \"Content-Type: application/json\"", curl);
        Assert.Contains("-d \"{\\\"name\\\":\\\"John\\\"}\"", curl);
    }
}
