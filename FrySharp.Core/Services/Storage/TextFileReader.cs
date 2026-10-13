using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Storage;

/// <summary>A text file as read for editing: its text, and how it was encoded so a save writes it back the same way.</summary>
public sealed record TextFileContent(string Text, Encoding Encoding);

/// <summary>Reads files for the editor: the encoding from the byte-order mark (UTF-8 without one), binary files refused.</summary>
public static class TextFileReader
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>True when the bytes look like a binary file (a NUL in the first 8 KB, as git decides).</summary>
    public static bool LooksBinary(ReadOnlySpan<byte> bytes)
    {
        var start = bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF))
            ? bytes.Length // UTF-16 text has NULs by design
            : 0;
        return bytes[start..Math.Max(start, Math.Min(bytes.Length, 8000))].Contains((byte)0);
    }

    /// <summary>Decodes a text file's bytes.</summary>
    public static TextFileContent Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new TextFileContent(Utf8NoBom.GetString(bytes, 3, bytes.Length - 3), Utf8WithBom);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return new TextFileContent(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Encoding.Unicode);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return new TextFileContent(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Encoding.BigEndianUnicode);
        }

        return new TextFileContent(Utf8NoBom.GetString(bytes), Utf8NoBom);
    }
}
