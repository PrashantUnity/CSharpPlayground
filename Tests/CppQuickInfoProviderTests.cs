using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>Unit tests for the static C++ Quick Info provider — no compiler or UI required.</summary>
public class CppQuickInfoProviderTests
{
    // ── Lookup ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("vector")]
    [InlineData("std::vector")]
    [InlineData("string")]
    [InlineData("cout")]
    [InlineData("sort")]
    [InlineData("unique_ptr")]
    [InlineData("optional")]
    [InlineData("nullptr")]
    [InlineData("constexpr")]
    public void Lookup_KnownSymbol_ReturnsInfo(string symbol)
    {
        var info = CppQuickInfoProvider.Lookup(symbol);
        Assert.NotNull(info);
        Assert.NotEmpty(info.Signature);
        Assert.NotEmpty(info.Summary);
    }

    [Fact]
    public void Lookup_StdPrefixed_StripsPrefixAndResolves()
    {
        var direct = CppQuickInfoProvider.Lookup("vector");
        var prefixed = CppQuickInfoProvider.Lookup("std::vector");

        Assert.NotNull(direct);
        Assert.NotNull(prefixed);
        Assert.Equal(direct.Signature, prefixed.Signature);
    }

    [Fact]
    public void Lookup_FryPrefixed_ResolvesDisplaySymbol()
    {
        var info = CppQuickInfoProvider.Lookup("fry::dump");
        Assert.NotNull(info);
        Assert.Contains("fry/display.hpp", info.Header ?? "");
    }

    [Fact]
    public void Lookup_UnknownSymbol_ReturnsNull()
    {
        var info = CppQuickInfoProvider.Lookup("xyzzy_not_a_real_symbol_9182736");
        Assert.Null(info);
    }

    [Fact]
    public void Lookup_EmptyString_ReturnsNull()
    {
        Assert.Null(CppQuickInfoProvider.Lookup(""));
    }

    [Fact]
    public void Lookup_Whitespace_ReturnsNull()
    {
        Assert.Null(CppQuickInfoProvider.Lookup("   "));
    }

    // ── Metadata correctness ──────────────────────────────────────────────

    [Fact]
    public void Lookup_Vector_HasCorrectHeader()
    {
        var info = CppQuickInfoProvider.Lookup("vector")!;
        Assert.Equal("<vector>", info.Header);
    }

    [Fact]
    public void Lookup_OptionalHasSince_Cpp17()
    {
        var info = CppQuickInfoProvider.Lookup("optional")!;
        Assert.Equal("C++17", info.Since);
    }

    [Fact]
    public void Lookup_Concept_IsC20()
    {
        var info = CppQuickInfoProvider.Lookup("concept")!;
        Assert.Equal("C++20", info.Since);
    }

    [Fact]
    public void Lookup_IntKeyword_HasNoHeader()
    {
        var info = CppQuickInfoProvider.Lookup("int")!;
        Assert.Null(info.Header);
    }

    [Fact]
    public void Lookup_Thread_HasThreadHeader()
    {
        var info = CppQuickInfoProvider.Lookup("thread")!;
        Assert.Equal("<thread>", info.Header);
    }

    // ── ExtractSymbol ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("std::vector<int> v;", 5, "std::vector")]   // cursor inside "vector"
    [InlineData("sort(v.begin(), v.end());", 2, "sort")]      // cursor in "sort"
    [InlineData("  cout << x;", 3, "cout")]                  // cursor in "cout"
    [InlineData("auto x = make_unique<T>();", 10, "make_unique")] // cursor in "make_unique"
    public void ExtractSymbol_ReturnsExpectedToken(string code, int position, string expected)
    {
        var symbol = CppQuickInfoProvider.ExtractSymbol(code, position);
        Assert.Equal(expected, symbol);
    }

    [Fact]
    public void ExtractSymbol_AtLessThanOperator_ReturnsPrecedingIdentifier()
    {
        // Position 6 is the '<' in "vector<int>". The extractor walks left to find
        // the nearest identifier (vector), which is the right hover target for template types.
        var symbol = CppQuickInfoProvider.ExtractSymbol("vector<int>", 6);
        Assert.Equal("vector", symbol);
    }

    [Fact]
    public void ExtractSymbol_AtSemicolon_ReturnsNull()
    {
        // ';' is not an identifier char and has no preceding identifier on the same position.
        // "int x;" — position 5 is ';', and position 4 is 'x' (identifier).
        // The correct behavior is still to return the ident before. So test a fully isolated case:
        var symbol = CppQuickInfoProvider.ExtractSymbol(";", 0);
        Assert.Null(symbol);
    }

    [Fact]
    public void ExtractSymbol_EmptyCode_ReturnsNull()
    {
        Assert.Null(CppQuickInfoProvider.ExtractSymbol("", 0));
    }

    [Fact]
    public void ExtractSymbol_NegativePosition_ReturnsNull()
    {
        Assert.Null(CppQuickInfoProvider.ExtractSymbol("vector", -1));
    }

    [Fact]
    public void ExtractSymbol_BeyondLength_ReturnsNull()
    {
        Assert.Null(CppQuickInfoProvider.ExtractSymbol("vec", 100));
    }

    // ── End-to-end lookup flow ────────────────────────────────────────────

    [Fact]
    public void ExtractAndLookup_StdCoutInRealCode_FindsInfo()
    {
        const string code = "std::cout << \"Hello\" << std::endl;";
        var symbol = CppQuickInfoProvider.ExtractSymbol(code, 5); // 'c' of cout
        Assert.NotNull(symbol);
        var info = CppQuickInfoProvider.Lookup(symbol!);
        Assert.NotNull(info);
        Assert.Contains("output", info!.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractAndLookup_SortAlgorithm_FindsInfo()
    {
        const string code = "std::sort(v.begin(), v.end());";
        var symbol = CppQuickInfoProvider.ExtractSymbol(code, 7); // cursor in 'sort'
        Assert.NotNull(symbol);
        var info = CppQuickInfoProvider.Lookup(symbol!);
        Assert.NotNull(info);
        Assert.Contains("<algorithm>", info!.Header ?? "");
    }

    [Fact]
    public void ExtractAndLookup_FryDump_FindsDisplayRuntime()
    {
        const string code = "#include <fry/display.hpp>\nfry::dump(result, \"Answer\");";
        // cursor on 'd' of 'dump' in second line.
        var symbol = CppQuickInfoProvider.ExtractSymbol(code, 32);
        Assert.NotNull(symbol);
        var info = CppQuickInfoProvider.Lookup(symbol!);
        Assert.NotNull(info);
    }
}
