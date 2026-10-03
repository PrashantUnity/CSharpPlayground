using System;
using System.Reflection;
using Avalonia.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;

/// <summary>
/// Factory that dynamically instantiates Avalonia controls from declarative XAML
/// or C# delegate factories, wrapping them in crash-resilient error boundaries.
/// </summary>
public static class DynamicViewFactory
{
    private static readonly MethodInfo? RuntimeXamlLoadMethod;

    static DynamicViewFactory()
    {
        try
        {
            var loaderType = Type.GetType("Avalonia.Markup.Xaml.Loader.AvaloniaRuntimeXamlLoader, Avalonia.Markup.Xaml.Loader")
                ?? Type.GetType("Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader, Avalonia.Markup.Xaml");
            RuntimeXamlLoadMethod = loaderType?.GetMethod("Load", new[] { typeof(string) });
        }
        catch
        {
            // Reflection fallback if loader assembly is not present
        }
    }

    /// <summary>
    /// Parses an Avalonia XAML snippet and returns a mounted Control protected by an error boundary.
    /// </summary>
    public static Control CreateFromXaml(string xaml, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);

        string effectiveXaml = EnsureDefaultNamespaces(xaml);

        return new ExtensibilityErrorBoundaryControl(() =>
        {
            if (RuntimeXamlLoadMethod == null)
            {
                throw new InvalidOperationException("AvaloniaRuntimeXamlLoader is not available in the current runtime.");
            }

            var result = RuntimeXamlLoadMethod.Invoke(null, new object[] { effectiveXaml });
            if (result is Control ctrl)
            {
                return ctrl;
            }
            if (result is object obj)
            {
                return new ContentControl { Content = obj };
            }

            throw new InvalidOperationException("XAML did not produce a visual Control.");
        }, title ?? "XAML Component");
    }

    /// <summary>
    /// Wraps a procedural C# control factory in an error boundary.
    /// </summary>
    public static Control CreateFromFactory(Func<object> factory, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new ExtensibilityErrorBoundaryControl(factory, title);
    }

    private static string EnsureDefaultNamespaces(string xaml)
    {
        string trimmed = xaml.Trim();
        if (!trimmed.Contains("xmlns=\"https://github.com/avaloniaui\"", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("xmlns='https://github.com/avaloniaui'", StringComparison.OrdinalIgnoreCase))
        {
            int firstSpace = trimmed.IndexOfAny(new[] { ' ', '>' });
            if (firstSpace > 1 && trimmed[0] == '<')
            {
                string tag = trimmed.Substring(0, firstSpace);
                string rest = trimmed.Substring(firstSpace);
                return $"{tag} xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"{rest}";
            }
        }
        return trimmed;
    }
}
