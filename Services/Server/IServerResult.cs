using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public interface IServerResult
{
    int StatusCode { get; }
    string ContentType { get; }
    Task ExecuteAsync(HttpListenerResponse response);
}

public class JsonResult : IServerResult
{
    public int StatusCode { get; }
    public string ContentType => "application/json";
    public object? Value { get; }

    public JsonResult(object? value, int statusCode = 200)
    {
        Value = value;
        StatusCode = statusCode;
    }

    public async Task ExecuteAsync(HttpListenerResponse response)
    {
        response.StatusCode = StatusCode;
        response.ContentType = ContentType;

        var json = Value == null
            ? "{}"
            : JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true });

        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }
}

public class TextResult : IServerResult
{
    public int StatusCode { get; }
    public string ContentType { get; }
    public string Content { get; }

    public TextResult(string content, int statusCode = 200, string contentType = "text/plain")
    {
        Content = content;
        StatusCode = statusCode;
        ContentType = contentType;
    }

    public async Task ExecuteAsync(HttpListenerResponse response)
    {
        response.StatusCode = StatusCode;
        response.ContentType = ContentType;
        var bytes = Encoding.UTF8.GetBytes(Content ?? string.Empty);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }
}

public class FileResult : IServerResult
{
    public int StatusCode => 200;
    public string ContentType { get; }
    public byte[] Data { get; }
    public string? DownloadFileName { get; }

    public FileResult(byte[] data, string contentType = "application/octet-stream", string? downloadFileName = null)
    {
        Data = data;
        ContentType = contentType;
        DownloadFileName = downloadFileName;
    }

    public async Task ExecuteAsync(HttpListenerResponse response)
    {
        response.StatusCode = StatusCode;
        response.ContentType = ContentType;
        if (!string.IsNullOrEmpty(DownloadFileName))
        {
            response.Headers["Content-Disposition"] = $"attachment; filename=\"{DownloadFileName}\"";
        }
        response.ContentLength64 = Data.Length;
        await response.OutputStream.WriteAsync(Data).ConfigureAwait(false);
    }
}

public class StatusCodeResult : IServerResult
{
    public int StatusCode { get; }
    public string ContentType => "application/json";
    public object? Error { get; }

    public StatusCodeResult(int statusCode, object? error = null)
    {
        StatusCode = statusCode;
        Error = error;
    }

    public async Task ExecuteAsync(HttpListenerResponse response)
    {
        response.StatusCode = StatusCode;
        if (Error != null)
        {
            response.ContentType = ContentType;
            var json = JsonSerializer.Serialize(Error, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }
    }
}

public class NextResult : IServerResult
{
    public int StatusCode => 0;
    public string ContentType => string.Empty;
    public Task ExecuteAsync(HttpListenerResponse response) => Task.CompletedTask;
}
