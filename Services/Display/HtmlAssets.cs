using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// Scripts the studio ships for HTML outputs (ECharts and echarts-gl). An output names one with
/// <c>&lt;script data-fry-asset="echarts.min.js"&gt;&lt;/script&gt;</c> instead of carrying a megabyte of library; the
/// view puts the script in when it shows the page (<see cref="ForDisplay"/>), and so does an export
/// (<see cref="Inline"/>). A saved notebook then keeps a few kilobytes per chart, not the library each time.
/// </summary>
public static partial class HtmlAssets
{
    /// <summary>The scripts there are, with where a browser can get each when the studio's copy can't be read.</summary>
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["echarts.min.js"] = "https://cdn.jsdelivr.net/npm/echarts@5.5.0/dist/echarts.min.js",
        ["echarts-gl.min.js"] = "https://cdn.jsdelivr.net/npm/echarts-gl@2.0.9/dist/echarts-gl.min.js"
    };

    private static readonly ConcurrentDictionary<string, string> Loaded = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("""<script data-fry-asset="(?<name>[A-Za-z0-9._-]+)"></script>""")]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"<head[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadRegex();

    /// <summary>The tag that stands for <paramref name="name"/> in an output's HTML.</summary>
    public static string Reference(string name)
    {
        if (!Known.ContainsKey(name)) throw new ArgumentException($"There is no studio script named '{name}'.", nameof(name));
        return $"<script data-fry-asset=\"{name}\"></script>";
    }

    /// <summary>True when the page names a studio script.</summary>
    public static bool HasReferences(string? html) => html != null && ReferenceRegex().IsMatch(html);

    /// <summary>
    /// The page with each named script written in, so it works on its own (saved to a file or opened in a browser). A
    /// script the studio can't read is loaded from its CDN instead.
    /// </summary>
    public static string Inline(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return ReferenceRegex().Replace(html, m =>
        {
            var name = m.Groups["name"].Value;
            var script = Load(name);
            if (script.Length > 0) return "<script>" + script + "</script>";
            return Known.TryGetValue(name, out var cdn) ? $"<script src=\"{cdn}\"></script>" : m.Value;
        });
    }

    /// <summary>
    /// The page as the studio shows it: its scripts written in, and <c>window.fryHost</c> set first, so the page can draw
    /// in the studio's colours (<c>{ theme: "dark" | "light", background: "#rrggbb" }</c>).
    /// </summary>
    public static string ForDisplay(string html, bool dark, string? background = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var host = $"<script>window.fryHost={{theme:\"{(dark ? "dark" : "light")}\"" +
                   (IsColor(background) ? $",background:\"{background}\"" : string.Empty) + "};</script>";
        var page = HasReferences(html) ? Inline(html) : html;
        var head = HeadRegex().Match(page);
        return head.Success ? page.Insert(head.Index + head.Length, host) : host + page;
    }

    private static bool IsColor(string? value) =>
        value is { Length: 7 or 9 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    // The script from the plugin's resources, or from Assets next to it; read once.
    private static string Load(string name) => Loaded.GetOrAdd(name, static n =>
    {
        try
        {
            var assembly = typeof(HtmlAssets).Assembly;
            using var stream = assembly.GetManifestResourceStream("ECharts." + n);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }

            var file = Path.Combine(Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory, "Assets", "echarts", n);
            return File.Exists(file) ? File.ReadAllText(file) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return string.Empty;
        }
    });
}
