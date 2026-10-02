using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class PolyglotDisplayRuntimeTests
{
    [Fact]
    public void JavaDisplayRuntime_GenerateDisplayJavaSource_GeneratesExpectedMethods()
    {
        var defaultPkgSource = JavaDisplayRuntime.GenerateDisplayJavaSource(null);
        Assert.DoesNotContain("package ", defaultPkgSource);
        Assert.Contains("public class Display", defaultPkgSource);
        Assert.Contains("public static <T> T dump(T obj)", defaultPkgSource);
        Assert.Contains("public static void table(String title, Object obj)", defaultPkgSource);
        Assert.Contains("public static void html(String htmlContent)", defaultPkgSource);
        Assert.Contains("public static void image(", defaultPkgSource);

        var namedPkgSource = JavaDisplayRuntime.GenerateDisplayJavaSource("com.fry.test");
        Assert.StartsWith("package com.fry.test;", namedPkgSource.TrimStart());
        Assert.Contains("public class Display", namedPkgSource);
    }

    [Fact]
    public async Task JavaDisplayRuntime_EnsureSourceFilesAsync_CreatesSourceFiles()
    {
        var temp = Path.Combine(Path.GetTempPath(), "FryStudioTests", "java_src_" + Guid.NewGuid().ToString("n"));
        try
        {
            var files = await JavaDisplayRuntime.EnsureSourceFilesAsync(temp, "demo.pkg");
            Assert.NotEmpty(files);
            foreach (var file in files)
            {
                Assert.True(File.Exists(file));
                var content = await File.ReadAllTextAsync(file);
                Assert.Contains("public class Display", content);
            }
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task PythonDisplayRuntime_EnsureRuntimeFilesAsync_CreatesSiteCustomizeAndFry()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var runtimeDir = await PythonDisplayRuntime.EnsureRuntimeFilesAsync(host);

        Assert.True(Directory.Exists(runtimeDir));
        var sitecustomize = Path.Combine(runtimeDir, "sitecustomize.py");
        var fry = Path.Combine(runtimeDir, "fry.py");

        Assert.True(File.Exists(sitecustomize));
        Assert.True(File.Exists(fry));

        var fryContent = await File.ReadAllTextAsync(fry);
        Assert.Contains("class Display:", fryContent);
        Assert.Contains("def dump(", fryContent);
        Assert.Contains("def display(", fryContent);
    }

    [Fact]
    public async Task JavaScriptDisplayRuntime_EnsureRuntimeFilesAsync_CreatesPreloadAndFry()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var runtimeDir = await JavaScriptDisplayRuntime.EnsureRuntimeFilesAsync(host);

        Assert.True(Directory.Exists(runtimeDir));
        var preload = Path.Combine(runtimeDir, "preload.js");
        var fry = Path.Combine(runtimeDir, "fry.js");

        Assert.True(File.Exists(preload));
        Assert.True(File.Exists(fry));

        var fryContent = await File.ReadAllTextAsync(fry);
        Assert.Contains("class Display", fryContent);
        Assert.Contains("function dump(", fryContent);
        Assert.Contains("function display(", fryContent);
    }

    [Fact]
    public async Task CppDisplayRuntime_EnsureIncludeDirectoryAsync_CreatesHeaders()
    {
        var temp = Path.Combine(Path.GetTempPath(), "FryStudioTests", "cpp_inc_" + Guid.NewGuid().ToString("n"));
        try
        {
            var includeDir = await PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp.CppDisplayRuntime.EnsureIncludeDirectoryAsync(temp);
            Assert.True(Directory.Exists(includeDir));

            var fryHeader = Path.Combine(includeDir, "fry", "display.hpp");
            var directHeader = Path.Combine(includeDir, "display.hpp");

            Assert.True(File.Exists(fryHeader));
            Assert.True(File.Exists(directHeader));

            var content = await File.ReadAllTextAsync(fryHeader);
            Assert.Contains("namespace fry", content);
            Assert.Contains("namespace display", content);
            Assert.Contains("class Display", content);
            Assert.Contains("void table(", content);
            Assert.Contains("void html(", content);
            Assert.Contains("void image(", content);
            Assert.Contains("__FRY_DISPLAY__", content);
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
