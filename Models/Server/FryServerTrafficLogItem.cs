using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// Represents a captured HTTP request and response event for live server telemetry.
/// </summary>
public class FryServerTrafficLogItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string QueryString { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 200;
    public double ElapsedMilliseconds { get; set; }
    public string ClientIp { get; set; } = "127.0.0.1";
    public string? MatchedCellId { get; set; }
    public string? ErrorMessage { get; set; }
    public long ResponseBytesLength { get; set; }

    /// <summary>Formatted summary string for log lists and status decks.</summary>
    public string DisplayText =>
        $"[{Timestamp:HH:mm:ss}] {Method} {Path}{(string.IsNullOrEmpty(QueryString) ? "" : "?" + QueryString)} -> {StatusCode} ({ElapsedMilliseconds:F1}ms)";
}
