using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

/// <summary>
/// Execution context injected into each .fryserver endpoint or middleware cell script.
/// </summary>
public class FryServerContext
{
    public IReadOnlyDictionary<string, string> Params { get; }
    public IReadOnlyDictionary<string, string> Query { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }

    public string Method { get; }
    public string Path { get; }
    public string BodyText { get; }
    public byte[] RequestBytes { get; }

    /// <summary>
    /// Shared state dictionary persisted across all cells and retained during hot-reloads.
    /// </summary>
    public IDictionary<string, object> State { get; }

    /// <summary>Quick access to common 'id' route parameter.</summary>
    public string Id => Params.TryGetValue("id", out var val) ? val : string.Empty;

    public FryServerContext(
        string method,
        string path,
        IReadOnlyDictionary<string, string>? pathParams,
        IReadOnlyDictionary<string, string>? queryParams,
        IReadOnlyDictionary<string, string>? headers,
        string bodyText,
        byte[]? requestBytes,
        IDictionary<string, object>? state)
    {
        Method = method ?? "GET";
        Path = path ?? "/";
        Params = pathParams ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Query = queryParams ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Headers = headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        BodyText = bodyText ?? string.Empty;
        RequestBytes = requestBytes ?? Array.Empty<byte>();
        State = state ?? new ConcurrentDictionary<string, object>();
    }

    public T ParamValue<T>(string name, T defaultValue = default!)
    {
        if (Params.TryGetValue(name, out var str))
        {
            return ConvertValue(str, defaultValue);
        }
        return defaultValue;
    }

    public T QueryValue<T>(string name, T defaultValue = default!)
    {
        if (Query.TryGetValue(name, out var str))
        {
            return ConvertValue(str, defaultValue);
        }
        return defaultValue;
    }

    public T? ReadJson<T>(JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(BodyText)) return default;
        return JsonSerializer.Deserialize<T>(BodyText, options ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static T ConvertValue<T>(string value, T defaultValue)
    {
        try
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter.CanConvertFrom(typeof(string)))
            {
                var converted = converter.ConvertFromInvariantString(value);
                return converted != null ? (T)converted : defaultValue;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    // Response helper factory methods
    public IServerResult Ok(object? data = null) => new JsonResult(data, 200);
    public IServerResult Json(object? data, int statusCode = 200) => new JsonResult(data, statusCode);
    public IServerResult NotFound(object? error = null) => new StatusCodeResult(404, error ?? new { error = "Not Found" });
    public IServerResult BadRequest(object? error = null) => new StatusCodeResult(400, error ?? new { error = "Bad Request" });
    public IServerResult StatusCode(int code, object? data = null) => new StatusCodeResult(code, data);
    public IServerResult Text(string text, int statusCode = 200, string contentType = "text/plain") => new TextResult(text, statusCode, contentType);
    public IServerResult Html(string html, int statusCode = 200) => new TextResult(html, statusCode, "text/html; charset=utf-8");
    public IServerResult File(byte[] bytes, string contentType = "application/octet-stream", string? filename = null) => new FileResult(bytes, contentType, filename);
    public IServerResult Next() => new NextResult();
}
