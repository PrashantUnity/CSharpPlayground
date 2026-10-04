using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class CppWindowsSdkResolverTests
{
    [Fact]
    public void NonWindowsHost_ReturnsEmptyResult()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var sdk = CppWindowsSdkResolver.Resolve(host);

        Assert.False(sdk.HasMsvcHeaders);
        Assert.False(sdk.HasMinGw);
        Assert.Empty(sdk.IncludeDirectories);
        Assert.Empty(sdk.LibDirectories);
    }

    [Fact]
    public void WindowsHost_ResolvesMsvcAndWindowsKitsDirectories()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["ProgramFiles"] = @"C:\Program Files";
        host.Variables["ProgramFiles(x86)"] = @"C:\Program Files (x86)";

        // Set up MSVC 2022 Community
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\include");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\lib\x64");

        // Set up Windows 10 SDK
        host.AddDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\ucrt");
        host.AddDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\shared");
        host.AddDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\um");
        host.AddDirectory(@"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\ucrt\x64");
        host.AddDirectory(@"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\um\x64");

        var sdk = CppWindowsSdkResolver.Resolve(host);

        Assert.True(sdk.HasMsvcHeaders);
        Assert.Contains(sdk.IncludeDirectories, p => p.Contains("MSVC") && p.EndsWith("include"));
        Assert.Contains(sdk.IncludeDirectories, p => p.Contains("10.0.22621.0") && p.EndsWith("ucrt"));
        Assert.Contains(sdk.IncludeDirectories, p => p.Contains("10.0.22621.0") && p.EndsWith("shared"));
        Assert.Contains(sdk.IncludeDirectories, p => p.Contains("10.0.22621.0") && p.EndsWith("um"));

        Assert.Contains(sdk.LibDirectories, p => p.Contains("MSVC") && (p.EndsWith(@"lib\x64") || p.EndsWith("lib/x64")));
        Assert.Contains(sdk.LibDirectories, p => p.Contains("10.0.22621.0") && (p.EndsWith(@"ucrt\x64") || p.EndsWith("ucrt/x64")));
        Assert.Contains(sdk.LibDirectories, p => p.Contains("10.0.22621.0") && (p.EndsWith(@"um\x64") || p.EndsWith("um/x64")));
    }

    [Fact]
    public void WindowsHost_ResolvesMinGwSysroot()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.AddDirectory(@"C:\msys64\ucrt64\include\c++");
        host.AddFile(@"C:\msys64\ucrt64\bin\g++.exe");

        var sdk = CppWindowsSdkResolver.Resolve(host);

        Assert.True(sdk.HasMinGw);
        Assert.Equal(@"C:\msys64\ucrt64", sdk.MinGwSysroot);
    }
}
