using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

/// <summary>
/// Ambient globals injected into every endpoint and middleware cell script.
/// </summary>
public class FryServerScriptGlobals
{
    public FryServerContext Context { get; set; } = null!;

    /// <summary>Convenient alias for route parameter 'id'.</summary>
    public string id => Context.Id;

    /// <summary>Access to named route parameters, e.g. PathParams["id"] or PathParams.GetInt("id").</summary>
    public PathParamAccessor PathParams => new(Context.Params);

    /// <summary>Access to query parameters, e.g. QueryParams["page"] or QueryParams.GetInt("page").</summary>
    public QueryParamAccessor QueryParams => new(Context.Query);

    /// <summary>Access to HTTP request headers.</summary>
    public IReadOnlyDictionary<string, string> Headers => Context.Headers;

    /// <summary>Access to request body text, bytes, or deserialized JSON.</summary>
    public RequestBodyAccessor Body => new(Context);

    /// <summary>ASP.NET Core Minimal API-style result factory (Results.Ok, Results.NotFound, Results.Created, etc.).</summary>
    public FryServerResultFactory Results => new(Context);

    /// <summary>Shared in-memory state dictionary across all cells.</summary>
    public IDictionary<string, object> State => Context.State;

    // Direct access to response helpers
    public IServerResult Ok(object? data = null) => Context.Ok(data);
    public IServerResult Json(object? data, int statusCode = 200) => Context.Json(data, statusCode);
    public IServerResult NotFound(object? error = null) => Context.NotFound(error);
    public IServerResult BadRequest(object? error = null) => Context.BadRequest(error);
    public IServerResult StatusCode(int code, object? data = null) => Context.StatusCode(code, data);
    public IServerResult Text(string text, int statusCode = 200, string contentType = "text/plain") => Context.Text(text, statusCode, contentType);
    public IServerResult Html(string html, int statusCode = 200) => Context.Html(html, statusCode);
    public IServerResult File(byte[] bytes, string contentType = "application/octet-stream", string? filename = null) => Context.File(bytes, contentType, filename);
    public IServerResult Next() => Context.Next();
}

public class FryServerResultFactory
{
    private readonly FryServerContext _context;
    public FryServerResultFactory(FryServerContext context) => _context = context;

    public IServerResult Ok(object? data = null) => _context.Ok(data);
    public IServerResult Json(object? data, int statusCode = 200) => _context.Json(data, statusCode);
    public IServerResult Created(string uri, object? data = null) => new StatusCodeResult(201, data);
    public IServerResult NotFound(object? error = null) => _context.NotFound(error);
    public IServerResult BadRequest(object? error = null) => _context.BadRequest(error);
    public IServerResult StatusCode(int code, object? data = null) => _context.StatusCode(code, data);
    public IServerResult Text(string text, int statusCode = 200, string contentType = "text/plain") => _context.Text(text, statusCode, contentType);
    public IServerResult Html(string html, int statusCode = 200) => _context.Html(html, statusCode);
    public IServerResult File(byte[] bytes, string contentType = "application/octet-stream", string? filename = null) => _context.File(bytes, contentType, filename);
}

public class PathParamAccessor
{
    private readonly IReadOnlyDictionary<string, string> _dict;
    public PathParamAccessor(IReadOnlyDictionary<string, string> dict) => _dict = dict;

    public string this[string key] => _dict.TryGetValue(key, out var v) ? v : string.Empty;
    public string Get(string key) => this[key];
    public int GetInt(string key, int defaultValue = 0) => int.TryParse(this[key], out var val) ? val : defaultValue;
    public long GetLong(string key, long defaultValue = 0) => long.TryParse(this[key], out var val) ? val : defaultValue;
    public bool TryGetValue(string key, out string value) => _dict.TryGetValue(key, out value!);
}

public class QueryParamAccessor
{
    private readonly IReadOnlyDictionary<string, string> _dict;
    public QueryParamAccessor(IReadOnlyDictionary<string, string> dict) => _dict = dict;

    public string this[string key] => _dict.TryGetValue(key, out var v) ? v : string.Empty;
    public string Get(string key) => this[key];
    public int GetInt(string key, int defaultValue = 0) => int.TryParse(this[key], out var val) ? val : defaultValue;
    public bool GetBool(string key, bool defaultValue = false) => bool.TryParse(this[key], out var val) ? val : defaultValue;
    public bool TryGetValue(string key, out string value) => _dict.TryGetValue(key, out value!);
}

public class RequestBodyAccessor
{
    private readonly FryServerContext _context;
    public RequestBodyAccessor(FryServerContext context) => _context = context;

    public string Text => _context.BodyText;
    public byte[] Bytes => _context.RequestBytes;
    public T? AsJson<T>(System.Text.Json.JsonSerializerOptions? options = null) => _context.ReadJson<T>(options);
    public System.Threading.Tasks.Task<T?> AsJsonAsync<T>(System.Text.Json.JsonSerializerOptions? options = null) => System.Threading.Tasks.Task.FromResult(_context.ReadJson<T>(options));
}
