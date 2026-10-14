namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// What only the host's UI can show: its own live elements and image types (on the desktop, Avalonia controls and
/// bitmaps). <see cref="Scripting.Display"/>, <c>Dump</c> and the notebook kernels ask it first, then show everything else
/// the same way in every host. A host without such types (the web server, the CLI) has none.
/// </summary>
public interface IScriptHostDisplay
{
    /// <summary>The output that shows <paramref name="value"/> when it is one of the host's live elements, or null.</summary>
    /// <exception cref="InvalidOperationException">The element can't be shown, with a message saying why (e.g. it belongs to the script's thread).</exception>
    RichCellOutput? TryCreateElementOutput(object value);

    /// <summary>The image as PNG bytes when it is one of the host's image types, or null.</summary>
    byte[]? TryEncodeImage(object value);

    /// <summary>How the Variables panel shows <paramref name="value"/> when it is one of the host's types, or null.</summary>
    HostValueDescription? Describe(object value);

    /// <summary>What a value of <paramref name="type"/> is when <c>#!share</c> can't send it ("a control"), or null.</summary>
    string? DescribeUnshareable(Type type);
}

/// <summary>A host value in the Variables panel: its text (<c>&lt;Avalonia.Button&gt;</c>) and kind (<c>UI Control</c>).</summary>
public readonly record struct HostValueDescription(string Text, string Kind);
