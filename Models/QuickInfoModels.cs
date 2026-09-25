using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>How a piece of hover text is drawn: the syntax colour of a signature part, or its role in the documentation.</summary>
public enum QuickInfoTextKind
{
    Text,
    /// <summary>Dim captions such as "(local variable)", "in class" and "(+ 2 overloads)".</summary>
    Label,
    Keyword,
    /// <summary>Classes, records and delegates.</summary>
    Type,
    /// <summary>Structs and enums.</summary>
    Struct,
    Interface,
    TypeParameter,
    Namespace,
    Method,
    /// <summary>Properties, fields and events.</summary>
    Member,
    /// <summary>Enum members and constants.</summary>
    Constant,
    /// <summary>Locals, parameters and range variables.</summary>
    Variable,
    StringLiteral,
    NumericLiteral,
    Punctuation,
    /// <summary>Inline code from <c>&lt;c&gt;</c> or a one-line <c>&lt;code&gt;</c>.</summary>
    Code,
    /// <summary>A multi-line <c>&lt;code&gt;</c> block, newlines kept.</summary>
    CodeBlock,
    Link,
    /// <summary>"\n" breaks the line; "\n\n" starts a new paragraph.</summary>
    LineBreak
}

public readonly record struct QuickInfoTextRun(string Text, QuickInfoTextKind Kind);

/// <summary>A documented name with its description: a parameter, a type parameter or a thrown exception.</summary>
public sealed record QuickInfoNamedSection(QuickInfoTextRun Name, IReadOnlyList<QuickInfoTextRun> Text);

/// <summary>An XML documentation comment, parsed into the sections the hover shows.</summary>
public sealed class QuickInfoDocumentation
{
    public static readonly QuickInfoDocumentation Empty = new();

    public IReadOnlyList<QuickInfoTextRun> Summary { get; init; } = [];
    public IReadOnlyList<QuickInfoNamedSection> TypeParameters { get; init; } = [];
    public IReadOnlyList<QuickInfoNamedSection> Parameters { get; init; } = [];
    public IReadOnlyList<QuickInfoTextRun> Returns { get; init; } = [];
    public IReadOnlyList<QuickInfoTextRun> Value { get; init; } = [];
    public IReadOnlyList<QuickInfoNamedSection> Exceptions { get; init; } = [];
    public IReadOnlyList<QuickInfoTextRun> Remarks { get; init; } = [];

    /// <summary>
    /// Set when the comment says <c>&lt;inheritdoc/&gt;</c>: its <c>cref</c>, or an empty string when it names none and the
    /// documentation comes from the overridden or implemented member.
    /// </summary>
    public string? InheritDocCref { get; init; }

    public bool IsEmpty =>
        Summary.Count == 0 && TypeParameters.Count == 0 && Parameters.Count == 0 && Returns.Count == 0 &&
        Value.Count == 0 && Exceptions.Count == 0 && Remarks.Count == 0;
}

/// <summary>What the editor shows while the pointer rests on a symbol: its signature, where it's declared and its documentation.</summary>
public sealed class CSharpQuickInfo
{
    /// <summary>Where the hovered token starts, in the offsets of the text that was analysed.</summary>
    public required int SpanStart { get; init; }

    public required int SpanLength { get; init; }

    /// <summary>The declaration line, e.g. <c>public static void WriteLine(string? value) (+ 17 overloads)</c>.</summary>
    public required IReadOnlyList<QuickInfoTextRun> Signature { get; init; }

    /// <summary>Where the symbol is declared, e.g. <c>in class System.Console</c>; empty for locals and script-level types.</summary>
    public IReadOnlyList<QuickInfoTextRun> Container { get; init; } = [];

    /// <summary>One line per type argument of a constructed type, e.g. <c>T is int</c>.</summary>
    public IReadOnlyList<IReadOnlyList<QuickInfoTextRun>> TypeArguments { get; init; } = [];

    public QuickInfoDocumentation Documentation { get; init; } = QuickInfoDocumentation.Empty;
}
