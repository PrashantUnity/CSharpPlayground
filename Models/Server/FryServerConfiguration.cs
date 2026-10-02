namespace PdfEditorApp.Plugins.CSharpEditor.Models.Server;

/// <summary>
/// Server configuration settings for a .fryserver document instance.
/// </summary>
public class FryServerConfiguration
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5000;
    public string Scheme { get; set; } = "http";
    public string ApiPrefix { get; set; } = "/api";
    public bool EnableCors { get; set; } = true;
    public List<string> CorsAllowedOrigins { get; set; } = new() { "*" };
    public bool AllowPrivateNetwork { get; set; } = true;
    public bool AutoStartOnOpen { get; set; } = false;
    public int SimulatedLatencyMs { get; set; } = 0;
    public bool AutoPortFallback { get; set; } = true;

    /// <summary>
    /// Gets the full base URL including scheme, host, port, and trimmed API prefix.
    /// </summary>
    public string BaseUrl
    {
        get
        {
            var prefix = string.IsNullOrWhiteSpace(ApiPrefix)
                ? string.Empty
                : (ApiPrefix.StartsWith("/") ? ApiPrefix : "/" + ApiPrefix).TrimEnd('/');
            return $"{Scheme}://{Host}:{Port}{prefix}";
        }
    }
}
