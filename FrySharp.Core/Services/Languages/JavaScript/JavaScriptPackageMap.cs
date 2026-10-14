namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Maps a required/imported module specifier to its npm package name (e.g. <c>lodash/debounce</c> -> <c>lodash</c>,
/// <c>@angular/core/testing</c> -> <c>@angular/core</c>).
/// </summary>
public static class JavaScriptPackageMap
{
    private static readonly HashSet<string> BuiltinModules = new(StringComparer.Ordinal)
    {
        "assert", "async_hooks", "buffer", "child_process", "cluster", "console", "constants",
        "crypto", "dgram", "diagnostics_channel", "dns", "domain", "events", "fs", "fs/promises",
        "http", "http2", "https", "inspector", "module", "net", "os", "path", "path/posix",
        "path/win32", "perf_hooks", "process", "punycode", "querystring", "readline", "repl",
        "stream", "stream/consumers", "stream/promises", "stream/web", "string_decoder",
        "sys", "timers", "timers/promises", "tls", "trace_events", "tty", "url", "util",
        "util/types", "v8", "vm", "wasi", "worker_threads", "zlib"
    };

    public static bool IsBuiltin(string moduleName)
    {
        var name = moduleName.Trim().Trim('\'', '"');
        if (name.StartsWith("node:", StringComparison.Ordinal)) name = name["node:".Length..];
        return BuiltinModules.Contains(name);
    }

    /// <summary>
    /// Gets the npm package name for an unresolved module specifier. Returns null if it's a relative path or built-in.
    /// </summary>
    public static string? PackageFor(string moduleName)
    {
        var name = moduleName.Trim().Trim('\'', '"');
        if (name.StartsWith("node:", StringComparison.Ordinal)) return null;
        if (name.StartsWith("./", StringComparison.Ordinal) || name.StartsWith("../", StringComparison.Ordinal) || name.StartsWith('/'))
        {
            return null; // Local relative import
        }

        if (IsBuiltin(name)) return null;

        // Scoped package: @scope/package/subpath -> @scope/package
        if (name.StartsWith('@') && name.Contains('/'))
        {
            var parts = name.Split('/');
            return parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : name;
        }

        // Regular package: lodash/debounce -> lodash
        var slash = name.IndexOf('/');
        return slash > 0 ? name[..slash] : name;
    }
}
