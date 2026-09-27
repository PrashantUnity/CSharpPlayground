using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Visualizers;

/// <summary>
/// Type visualizer for HTTP requests, responses, content, headers, and status codes.
/// Extracts safe previews, flattens headers, and detects SSE event streams.
/// </summary>
public sealed class HttpTypeVisualizer : IDebugTypeVisualizer
{
    public int Priority => 100;

    public bool CanVisualize(object? value, Type type) =>
        HttpInspectionHelper.IsHttpType(value);

    public string FormatSummary(object value, Type type) => value switch
    {
        HttpResponseMessage resp => HttpInspectionHelper.FormatResponseSummary(resp),
        HttpRequestMessage req => HttpInspectionHelper.FormatRequestSummary(req),
        HttpContent content => HttpInspectionHelper.FormatContentSummary(content),
        HttpHeaders headers => HttpInspectionHelper.FormatHeadersSummary(headers),
        HttpStatusCode code => HttpInspectionHelper.FormatStatusCodeSummary(code),
        _ => value.ToString() ?? type.Name
    };

    public IEnumerable<(string Name, object? Value)> GetChildren(object value, Type type) => value switch
    {
        HttpResponseMessage resp => HttpInspectionHelper.GetResponseChildren(resp),
        HttpRequestMessage req => HttpInspectionHelper.GetRequestChildren(req),
        HttpContent content => HttpInspectionHelper.GetContentChildren(content),
        HttpHeaders headers => HttpInspectionHelper.GetHeaderChildren(headers),
        _ => Enumerable.Empty<(string Name, object? Value)>()
    };
}
