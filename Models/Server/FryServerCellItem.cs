using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// A polymorphic cell within a .fryserver document.
/// </summary>
public class FryServerCellItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public FryServerCellType Type { get; set; } = FryServerCellType.Endpoint;
    public string Title { get; set; } = "New Endpoint";
    public bool Enabled { get; set; } = true;
    public string Language { get; set; } = "csharp";

    // Endpoint specific metadata
    public string Method { get; set; } = "GET"; // GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, ANY
    public string Route { get; set; } = "/";
    public string Description { get; set; } = string.Empty;
    public int DefaultStatusCode { get; set; } = 200;
    public string ResponseContentType { get; set; } = "application/json";
    public int SimulatedLatencyMs { get; set; } = 0;

    // Source code or Markdown text
    public string Source { get; set; } = string.Empty;

    // Test Harness (for in-cell interactive loopback testing)
    public FryServerTestHarnessItem TestHarness { get; set; } = new();

    // Ephemeral / Runtime telemetry
    public int RequestCount { get; set; }
    public double LastExecutionTimeMs { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastResponseText { get; set; }
    public string? LastErrorText { get; set; }

    public bool IsEndpoint => Type == FryServerCellType.Endpoint;
    public bool IsStartup => Type == FryServerCellType.Startup;
    public bool IsMiddleware => Type == FryServerCellType.Middleware;
    public bool IsScenario => Type == FryServerCellType.Scenario;
    public bool IsMarkdown => Type == FryServerCellType.Markdown;
}
