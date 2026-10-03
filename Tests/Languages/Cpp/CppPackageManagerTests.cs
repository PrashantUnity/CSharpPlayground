using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class CppPackageManagerTests
{
    private readonly CppPackageManager _manager = new();

    [Theory]
    [InlineData("// #vcpkg: nlohmann-json", new[] { "install", "nlohmann-json" })]
    [InlineData("//#vcpkg: fmt", new[] { "install", "fmt" })]
    [InlineData("%vcpkg install spdlog", new[] { "install", "spdlog" })]
    [InlineData("!vcpkg add cxxopts", new[] { "install", "cxxopts" })]
    [InlineData("#r \"vcpkg: eigen3\"", new[] { "install", "eigen3" })]
    [InlineData("#r \"vcpkg:opencv4\"", new[] { "install", "opencv4" })]
    public void ValidDirectives_ParseCorrectly(string line, string[] expectedArgs)
    {
        var ok = _manager.TryParseDirective(line, out var command);

        Assert.True(ok);
        Assert.Equal(expectedArgs, command.Arguments);
    }

    [Theory]
    [InlineData("%pip install numpy")]
    [InlineData("%maven install com.google.code.gson:gson:2.11.0")]
    [InlineData("#r \"nuget: Newtonsoft.Json\"")]
    [InlineData("int x = 10;")]
    [InlineData("// normal comment")]
    [InlineData("%vcpkg")]
    public void InvalidDirectives_AreRejected(string line)
    {
        var ok = _manager.TryParseDirective(line, out _);
        Assert.False(ok);
    }

    [Fact]
    public void InstallCommand_GeneratesProperCppDirective()
    {
        var cmd = _manager.InstallCommand("nlohmann-json");

        Assert.Equal("// #vcpkg: nlohmann-json", cmd.Text);
        Assert.Equal(new[] { "install", "nlohmann-json" }, cmd.Arguments);
    }

    [Theory]
    [InlineData("nlohmann/json.hpp", "nlohmann-json")]
    [InlineData("fmt/core.h", "fmt")]
    [InlineData("fmt/format.h", "fmt")]
    [InlineData("spdlog/spdlog.h", "spdlog")]
    [InlineData("cxxopts.hpp", "cxxopts")]
    [InlineData("Eigen/Dense", "eigen3")]
    [InlineData("boost/algorithm/string.hpp", "boost")]
    [InlineData("catch2/catch_test_macros.hpp", "catch2")]
    [InlineData("opencv2/opencv.hpp", "opencv4")]
    [InlineData("custom_header.h", "custom_header.h")]
    public void PackageForMissingDependency_ResolvesHeadersToVcpkgPackages(string header, string expectedPackage)
    {
        Assert.Equal(expectedPackage, _manager.PackageForMissingDependency(header));
    }

    [Fact]
    public void ClangGccDiagnosticParser_ExtractsMissingDependency_FromClangFileNotFound()
    {
        var parser = new ClangGccDiagnosticParser();
        const string output = """
            /work/main.cpp:3:10: fatal error: 'nlohmann/json.hpp' file not found
            #include <nlohmann/json.hpp>
                     ^~~~~~~~~~~~~~~~~~~
            1 error generated.
            """;

        var result = parser.Parse(output, "/work/main.cpp");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, result.Diagnostics[0].Severity);
        Assert.Equal("nlohmann/json.hpp", result.MissingDependency);
    }

    [Fact]
    public void ClangGccDiagnosticParser_ExtractsMissingDependency_FromGccNoSuchFile()
    {
        var parser = new ClangGccDiagnosticParser();
        const string output = """
            /work/main.cpp:2:10: fatal error: fmt/core.h: No such file or directory
                2 | #include <fmt/core.h>
                  |          ^~~~~~~~~~~~
            compilation terminated.
            """;

        var result = parser.Parse(output, "/work/main.cpp");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, result.Diagnostics[0].Severity);
        Assert.Equal("fmt/core.h", result.MissingDependency);
    }
}
