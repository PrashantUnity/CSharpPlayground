using System.Collections.Frozen;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Turns the crate name a compiler error mentions into the package to add. A crate is used in code with underscores
/// (<c>tokio_stream</c>) but published with hyphens (<c>tokio-stream</c>); and a few crates are useless without a feature
/// (<c>serde</c> without <c>derive</c>), so those are added with the one nearly everyone wants.
/// </summary>
public static class RustPackageMap
{
    private static readonly FrozenDictionary<string, string> Packages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["async_trait"] = "async-trait",
        ["tokio_stream"] = "tokio-stream",
        ["tokio_util"] = "tokio-util",
        ["futures_util"] = "futures-util",
        ["futures_core"] = "futures-core",
        ["crossbeam_channel"] = "crossbeam-channel",
        ["crossbeam_utils"] = "crossbeam-utils",
        ["crossbeam_queue"] = "crossbeam-queue",
        ["crossbeam_deque"] = "crossbeam-deque",
        ["proc_macro2"] = "proc-macro2",
        ["num_traits"] = "num-traits",
        ["num_bigint"] = "num-bigint",
        ["num_integer"] = "num-integer",
        ["num_complex"] = "num-complex",
        ["tower_http"] = "tower-http",
        ["actix_web"] = "actix-web",
        ["actix_rt"] = "actix-rt",
        ["tracing_subscriber"] = "tracing-subscriber",
        ["tracing_appender"] = "tracing-appender",
        ["hyper_util"] = "hyper-util",
        ["http_body_util"] = "http-body-util",
        ["console_subscriber"] = "console-subscriber",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // What to write on the right of "name = " for a crate that is only useful with a feature, or that a script always wants pinned.
    private static readonly FrozenDictionary<string, string> DefaultValues = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["serde"] = "{ version = \"1\", features = [\"derive\"] }",
        ["tokio"] = "{ version = \"1\", features = [\"full\"] }",
        ["clap"] = "{ version = \"4\", features = [\"derive\"] }",
        ["uuid"] = "{ version = \"1\", features = [\"v4\"] }",
        ["reqwest"] = "{ version = \"0.12\", features = [\"blocking\", \"json\"] }",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary><c>tokio_stream</c> → <c>tokio-stream</c>; a name with no special case is returned as it is.</summary>
    public static string PackageFor(string codeName) =>
        Packages.TryGetValue(codeName.Trim(), out var package) ? package : codeName.Trim();

    /// <summary>The comment that adds a package to a file: its feature-enabled form where it needs one, otherwise pinned to <paramref name="version"/>.</summary>
    public static string CrateLine(string package, string version) =>
        $"// #crate: {package} = {(DefaultValues.TryGetValue(package, out var value) ? value : $"\"{version}\"")}";

    /// <summary>The TOML value to add a package with: a feature-enabled table for the few that need one, otherwise any version.</summary>
    public static string DefaultValue(string package) =>
        DefaultValues.TryGetValue(package, out var value) ? value : "\"*\"";
}
