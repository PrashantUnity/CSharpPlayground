namespace PdfEditorApp.Plugins.CSharpEditor.Models.Server;

/// <summary>
/// Holds parameters and payload for testing an endpoint cell within the studio.
/// </summary>
public class FryServerTestHarnessItem
{
    public Dictionary<string, string> PathParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> QueryParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Body { get; set; } = string.Empty;
    public string BodyContentType { get; set; } = "application/json";

    public FryServerTestHarnessItem Clone() => new()
    {
        PathParams = new(PathParams, StringComparer.OrdinalIgnoreCase),
        QueryParams = new(QueryParams, StringComparer.OrdinalIgnoreCase),
        Headers = new(Headers, StringComparer.OrdinalIgnoreCase),
        Body = Body,
        BodyContentType = BodyContentType
    };
}
