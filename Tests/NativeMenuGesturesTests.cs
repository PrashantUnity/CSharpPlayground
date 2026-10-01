using System;
using Avalonia.Input;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class NativeMenuGesturesTests
{
    [Theory]
    [InlineData("Meta+OemComma")]
    [InlineData("Meta+N")]
    [InlineData("Shift+Meta+N")]
    [InlineData("Meta+O")]
    [InlineData("Shift+Meta+O")]
    [InlineData("Meta+S")]
    [InlineData("Shift+Meta+S")]
    [InlineData("Alt+Meta+S")]
    [InlineData("Meta+W")]
    [InlineData("Shift+Meta+W")]
    [InlineData("Meta+Z")]
    [InlineData("Shift+Meta+Z")]
    [InlineData("Meta+X")]
    [InlineData("Meta+C")]
    [InlineData("Meta+V")]
    [InlineData("Meta+A")]
    [InlineData("Meta+F")]
    [InlineData("Alt+Meta+F")]
    [InlineData("Shift+Meta+F")]
    [InlineData("Shift+Alt+F")]
    [InlineData("Shift+Meta+P")]
    [InlineData("Shift+Meta+E")]
    [InlineData("Shift+Meta+D")]
    [InlineData("Shift+Meta+X")]
    [InlineData("Shift+Meta+M")]
    [InlineData("Meta+B")]
    [InlineData("Meta+J")]
    [InlineData("Meta+H")]
    [InlineData("F5")]
    [InlineData("Control+F5")]
    [InlineData("Shift+F5")]
    [InlineData("F10")]
    [InlineData("F11")]
    [InlineData("F9")]
    [InlineData("Meta+M")]
    public void NativeMenuGesture_ParsesSuccessfully(string gesture)
    {
        var parsed = KeyGesture.Parse(gesture);
        Assert.NotNull(parsed);
    }
}
