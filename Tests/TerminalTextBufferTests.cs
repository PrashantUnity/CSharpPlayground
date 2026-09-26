using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>Program output reads the way a terminal would show it.</summary>
public class TerminalTextBufferTests
{
    private static string Show(params string[] chunks)
    {
        var buffer = new TerminalTextBuffer();
        foreach (var chunk in chunks) buffer.Append(chunk);
        return buffer.Text;
    }

    [Fact]
    public void PlainText_IsKeptAsWritten() =>
        Assert.Equal("hello\nworld", Show("hel", "lo\nwor", "ld"));

    [Fact]
    public void CarriageReturn_RedrawsTheLine_LikeAProgressBar() =>
        Assert.Equal("done 100%\nnext", Show("done  10%\rdone  50%\r", "done 100%\n", "next"));

    [Fact]
    public void CrLf_SplitAcrossChunks_IsOneLineEnd() =>
        Assert.Equal("a\nb\n", Show("a\r", "\nb\r\n"));

    [Fact]
    public void ColorCodes_AreDropped() =>
        Assert.Equal("red plain", Show("\u001b[31mred\u001b[0m", " plain"));

    [Fact]
    public void EraseToEndOfLine_ShortensARedrawnLine() =>
        Assert.Equal("ok", Show("loading.....\rok\u001b[K"));

    [Fact]
    public void Backspace_StepsBackOverACharacter() =>
        Assert.Equal("aXc", Show("abc\b\bX"));

    [Fact]
    public void TitleSequences_AreDropped() =>
        Assert.Equal("text", Show("\u001b]0;window title\a", "te", "\u001b]2;other\u001b\\xt"));

    [Fact]
    public void LongOutput_KeepsTheNewestLinesAndSaysSo()
    {
        var buffer = new TerminalTextBuffer(maxLength: 4096);
        for (var i = 0; i < 2000; i++) buffer.Append($"line {i}\n");

        var text = buffer.Text;
        Assert.StartsWith(TerminalTextBuffer.TrimmedMarker, text, StringComparison.Ordinal);
        Assert.EndsWith("line 1999\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("line 0\n", text, StringComparison.Ordinal);
        Assert.True(text.Length <= 4096 + TerminalTextBuffer.TrimmedMarker.Length);
        // What's kept starts at the beginning of a line.
        Assert.StartsWith("line ", text[TerminalTextBuffer.TrimmedMarker.Length..], StringComparison.Ordinal);
    }
}
