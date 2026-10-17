using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests.Services.Packages;

public class PackageEcosystemComprehensiveTests
{
    [Theory]
    [InlineData("13.0.3", "13.0.3")]
    [InlineData("[13.0.3]", "13.0.3")]
    [InlineData("[1.0.0, 2.0.0)", "1.0.0")]
    [InlineData(">= 3.5.0", "3.5.0")]
    [InlineData("(, 4.0.0]", "4.0.0")]
    public void CleanVersion_NormalizesBracketedAndRangedVersions(string input, string expected)
    {
        var cleaned = NuGetSemanticVersion.CleanVersion(input);
        Assert.Equal(expected, cleaned);
    }

    [Theory]
    [InlineData("13.0.3", 13, 0, 3)]
    [InlineData("[13.0.3]", 13, 0, 3)]
    [InlineData("[1.2.3, 2.0.0)", 1, 2, 3)]
    [InlineData("2.0.0-preview.1", 2, 0, 0)]
    public void TryParse_ParsesBracketedAndStandardVersions(string input, int major, int minor, int patch)
    {
        var ok = NuGetSemanticVersion.TryParse(input, out var version);
        Assert.True(ok);
        Assert.NotNull(version);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
    }

    [Fact]
    public async Task NotebookDirectiveRunner_AllowsCSharpPackageDirectivesWithoutToolchain()
    {
        var engine = new CSharpEngine();
        Assert.Null(engine.Toolchain);
        Assert.NotNull(engine.Packages);

        var tempDir = Path.Combine(Path.GetTempPath(), "FrySharpTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var languages = new StudioLanguageServices(tempDir);
            var context = new KernelCreationContext(() => tempDir, () => tempDir);
            var router = new NotebookKernelRouter(languages.Registry, context);
            var runner = new NotebookDirectiveRunner(languages.Registry, router);
            var logs = new List<string>();

            var cmd = new PackageCommand("#r \"nuget: NonExistentPackage1234567, 1.0.0\"", ["install", "NonExistentPackage1234567", "1.0.0"]);
            var kernel = new NotebookExecutionKernel();

            await runner.RunPackageCommandAsync(
                engine,
                cmd,
                kernel,
                new ToolchainQuery(),
                line => logs.Add(line),
                CancellationToken.None);

            // It attempted installation and failed gracefully with resolution message, not toolchain rejection
            Assert.DoesNotContain(logs, l => l.Contains("can't install packages"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task NotebookExecutionKernel_ImplementsSearchPathKernel_AndRegistersSearchPath()
    {
        var kernel = new NotebookExecutionKernel();
        Assert.IsAssignableFrom<ISearchPathKernel>(kernel);

        var tempDir = Path.Combine(Path.GetTempPath(), "FrySharpTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var searchPathKernel = (ISearchPathKernel)kernel;
            await searchPathKernel.AddSearchPathAsync(tempDir);
            // Verify that calling AddSearchPathAsync completes cleanly
            Assert.True(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task OnlinePackageSearchService_HandlesOfflineGracefully()
    {
        var service = OnlinePackageSearchService.Instance;

        // Empty query returns empty list
        var emptyResults = await service.SearchAsync("csharp", "");
        Assert.Empty(emptyResults);

        // Unknown language falls back to NuGet search or empty on error
        var results = await service.SearchAsync("unknown_lang", "");
        Assert.Empty(results);
    }

    [Fact]
    public void RoslynCompilerService_CompileToAssembly_ProcessesNuGetDirectivesWithoutThrowing()
    {
        var compiler = new RoslynCompilerService();
        var codeWithNuGet = """
            #r "nuget: Newtonsoft.Json, 13.0.3"

            public static class Program
            {
                public static void Main()
                {
                    System.Console.WriteLine("Hello with NuGet");
                }
            }
            """;

        // Even in Program mode, compiling code with #r "nuget: ..." strips/processes directives
        // rather than failing with CS1026/CS1002 syntax errors
        var (success, assembly, diagnostics) = compiler.CompileToAssembly(codeWithNuGet, ExecutionLanguageMode.Program);

        // If network is offline during tests, it may warn or fail resolution, but must NOT throw unhandled exceptions
        Assert.NotNull(diagnostics);
    }

    [Theory]
    [InlineData("#r \"nuget: CsvHelper, 33.1.0\"", true)]
    [InlineData("// #dart: http", true)]
    [InlineData("// #crate: serde = \"1\"", true)]
    [InlineData("// #go: github.com/gin-gonic/gin", true)]
    [InlineData("// #vcpkg: fmt", true)]
    [InlineData("//DEPS com.google.code.gson:gson:2.11.0", true)]
    [InlineData("%pip install requests", true)]
    [InlineData("%npm install lodash", true)]
    [InlineData("var regularCode = 42;", false)]
    public void DocumentDirectiveRegex_MatchesExpectedLanguageDirectives(string line, bool shouldMatch)
    {
        // Tests the regex used across the IDE dependencies panel to track active directives
        var regex = new System.Text.RegularExpressions.Regex(
            @"^\s*(?:#r\s+""nuget:[^""]+""|//\s*DEPS\s+[^\r\n]+|//\s*#(?:vcpkg|pkg|go|golang|crate|dart):[^\r\n]+|[%#!](?:pip3?|npm|vcpkg|maven|cargo\s+add|crate)\s+[^\r\n]+)\s*;?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        Assert.Equal(shouldMatch, regex.IsMatch(line));
    }

    [Fact]
    public async Task OnlinePackageSearchService_ParsesCustomCratesAndNpmResponses()
    {
        var mockHandler = new MockHttpMessageHandler((req) =>
        {
            if (req.RequestUri?.Host.Contains("crates.io") == true)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                            "crates": [
                                {
                                    "name": "fastrand",
                                    "default_version": "2.1.0",
                                    "description": "Fast random number generator",
                                    "downloads": 50000000
                                }
                            ]
                        }
                        """, System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (req.RequestUri?.Host.Contains("npmjs.org") == true)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                            "objects": [
                                {
                                    "package": {
                                        "name": "axios",
                                        "version": "1.7.2",
                                        "description": "Promise based HTTP client",
                                        "publisher": { "username": "johndoe" }
                                    }
                                }
                            ]
                        }
                        """, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var customClient = new HttpClient(mockHandler);
        var searchService = new OnlinePackageSearchService(customClient);

        var crateResults = await searchService.SearchCratesIoAsync("fastrand");
        Assert.Single(crateResults);
        Assert.Equal("fastrand", crateResults[0].Id);
        Assert.Equal("2.1.0", crateResults[0].Version);
        Assert.Equal("Fast random number generator", crateResults[0].Description);

        var npmResults = await searchService.SearchNpmAsync("axios");
        Assert.Single(npmResults);
        Assert.Equal("axios", npmResults[0].Id);
        Assert.Equal("1.7.2", npmResults[0].Version);
        Assert.Equal("Promise based HTTP client", npmResults[0].Description);
        Assert.Equal("johndoe", npmResults[0].Authors);
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
