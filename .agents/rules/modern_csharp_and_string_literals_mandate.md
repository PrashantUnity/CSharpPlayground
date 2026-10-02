# Modern C# & Raw String Literals Mandate

This document defines the strict standard for string representations across the **C# Code Studio** (`CSharpPlayground`) codebase.

---

## 1. Core Rule: Raw String Literals (`""" ... """`)
The codebase targets modern **.NET 10 (C# 13)**. All contributors and AI agents must actively leverage modern C# language features:

- **Raw String Literals (`""" ... """`)**:
  Always use C# 11+ raw string literals for:
  - Multi-line code snippets (C#, Python, Rust, C++, Java, JS, Go, SQL).
  - Unit test code payloads and assertions.
  - JSON schemas, JSON configs, and JSON payloads.
  - XML/HTML documents and templates.
  - Regular expression patterns and multi-line error/traceback strings.

- **Strictly Prohibited: Clumsy Verbatim Strings with Escaped Quotes**:
  - **NEVER** write `@"#r ""nuget: ...""` with doubled quotes `""`.
  - **NEVER** write `@"Console.WriteLine(""Hello"");"`.
  - **NEVER** assemble multi-line strings with string concatenation chains (`+ "\n" +`).

- **Interpolated Raw String Literals (`$$"""..."""`)**:
  When embedding code, JSON, or regex patterns that contain curly braces `{}`:
  - Use `$$""" ... """` (or `$$$""" ... """`) so single or double curly braces are treated as literal characters without awkward backslash escaping.
  - Only variables preceded by the specified number of `$` signs (e.g. `{{variable}}`) are interpolated.

---

## 2. Examples

### Bad (Avoid):
```csharp
// Clumsy verbatim strings with escaped quotes
var code = @"#r ""nuget: SkiaSharp, 3.119.4""
using SkiaSharp;
Console.WriteLine(""Hello"");";
```

### Good (Mandated):
```csharp
// Clean C# raw string literals
var code = """
    #r "nuget: SkiaSharp, 3.119.4"
    using SkiaSharp;
    Console.WriteLine("Hello");
    """;
```
