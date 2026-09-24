using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildFundamentalsCategory()
    {
        return new DocCategory
        {
            Id = "fundamentals_csharp",
            Title = "C# Fundamentals",
            IconKind = MaterialIconKind.VariableBox,
            AccentColor = "#34D399",
            Badge = "Core",
            Description = "The building blocks of C#: types, control flow, methods, and null handling.",
            Articles = new List<DocArticle>
            {
                CreateFundamentalsArticle()
            }
        };
    }

    private DocArticle CreateFundamentalsArticle()
    {
        return new DocArticle
        {
            Id = "learn_fundamentals",
            Title = "C# Fundamentals: Types, Flow, Methods & Null",
            Subtitle = "The core vocabulary every C# program is built from — before you can write scripts, you need these basics down cold.",
            ReadingTime = "8 min read",
            Summary = "This chapter is the foundation for everything else in Learn C#: how data is stored (value vs reference), how to declare and name it, how to branch and loop, how methods pass arguments, and how to represent 'no value' safely with null and Nullable<T>.",
            Keywords = new List<string> { "data types", "value type", "reference type", "var", "const", "if", "switch", "loop", "for", "foreach", "while", "ref", "out", "in", "parameters", "try catch finally", "exception", "enum", "struct", "readonly", "null", "nullable", "null-conditional", "null-coalescing" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Data Types: Value vs Reference",
                    Content = "Every type in C# is either a value type or a reference type, and the difference controls how assignment, copying, and equality behave. Value types (int, double, bool, char, decimal, struct, enum) store their data directly wherever the variable lives — on the stack for a local, or inline inside an object for a field. Assigning one value-type variable to another copies the data. Reference types (class, string, array, interface, delegate) store a reference to data that lives on the managed heap; assigning one reference-type variable to another copies the pointer, so both variables end up pointing at the same object.",
                    BulletPoints = new List<string>
                    {
                        "Value types: int, long, double, float, decimal, bool, char, struct, enum — copied on assignment.",
                        "Reference types: class, string, object, array, interface, delegate — assignment copies the reference, not the data.",
                        "string behaves like a value type in practice because it is immutable, even though it is technically a reference type."
                    },
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "When a bug looks like 'I changed one variable and another one changed too', it's almost always a reference type being shared where a copy was expected."
                },
                new()
                {
                    Heading = "Variables, Constants & var",
                    Content = "A variable is declared with an explicit type (int count = 0;) or inferred with var, which asks the compiler to figure out the type from the right-hand side at compile time — var is not 'dynamic' or loosely typed, the variable still has a fixed, known type, just written for you. A constant declared with const is a compile-time value that can never change and must be assigned at declaration; readonly (covered later in this article) is the runtime equivalent for fields.",
                    BulletPoints = new List<string>
                    {
                        "var count = 5; is exactly equivalent to int count = 5; — both are strongly typed.",
                        "Use var when the type is obvious from the right-hand side (var list = new List<string>();); prefer an explicit type when it improves readability.",
                        "const int MaxRetries = 3; must be known at compile time — no method calls, no user input."
                    }
                },
                new()
                {
                    Heading = "Control Flow: if, switch & Loops",
                    Content = "C# offers the usual branching and looping constructs: if/else if/else for conditional branches, switch statements or switch expressions for matching one value against many possibilities, and for, foreach, while, and do-while for repetition. Modern C# switch expressions (value switch { pattern => result }) are often more concise than a chain of if/else if, and support pattern matching against types, ranges, and conditions with when guards.",
                    BulletPoints = new List<string>
                    {
                        "for — best when you need an index or a fixed number of iterations.",
                        "foreach — best for iterating any IEnumerable<T> (lists, arrays, dictionaries) without manual indexing.",
                        "while — repeats while a condition holds, checked before each iteration.",
                        "do-while — same as while, but guarantees the body runs at least once."
                    }
                },
                new()
                {
                    Heading = "Methods & Parameters: ref, out & in",
                    Content = "Method parameters are passed by value by default — the method receives a copy of the argument (a copy of the value for a value type, or a copy of the reference for a reference type). The ref, out, and in modifiers change this: ref passes the variable itself by reference so the method can read and write it (the caller must initialize it first); out also passes by reference but is meant for a method to hand a value back (the caller doesn't need to initialize it, but the method must assign it before returning); in passes a value type by reference for performance but forbids the method from modifying it.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "int.TryParse(text, out int number) is the most common use of out in everyday code — it lets a method return both a success flag and a parsed value without throwing an exception."
                },
                new()
                {
                    Heading = "Exception Handling: try-catch-finally (Preview)",
                    Content = "Wrap risky code in a try block, handle specific failures in one or more catch blocks (most specific exception type first), and use finally for cleanup code that must run whether or not an exception occurred. This is intentionally just a pointer here — a dedicated article later in this documentation goes deep on exception hierarchies, custom exceptions, and when to catch versus let an exception propagate.",
                    BulletPoints = new List<string>
                    {
                        "try { risky() } catch (FormatException ex) { ... } finally { cleanup() }",
                        "Catch the most specific exception type you can meaningfully handle — avoid bare catch (Exception) unless you're logging and rethrowing.",
                        "finally always runs, even if the try block returns or throws — ideal for closing files, connections, or releasing locks."
                    }
                },
                new()
                {
                    Heading = "enum, struct & readonly",
                    Content = "An enum defines a closed set of named integer constants, making code that would otherwise use 'magic numbers' self-documenting (Status.Active instead of 1). A struct is a lightweight value type, useful for small, immutable-ish data bundles (a Point, a Money amount) where copy semantics are actually desirable and heap allocation would be wasteful. readonly marks a field that can only be assigned in its declaration or in a constructor — after that it's fixed for the lifetime of the object, which is a lighter-weight guarantee than const because the value can be computed at runtime.",
                    BulletPoints = new List<string>
                    {
                        "enum Status { Pending, Active, Closed } — backed by int by default, but any integral type can be specified.",
                        "struct Point { public int X; public int Y; } — copied by value, so mutating a copy never affects the original.",
                        "readonly string _connectionString; can be set in a constructor from configuration, unlike const."
                    }
                },
                new()
                {
                    Heading = "null, Nullable<T>, ?. & ??",
                    Content = "Reference-type variables can be null, meaning 'no object'. Value types cannot be null on their own, but Nullable<T> (written T?, e.g. int?) wraps a value type to add that possibility, exposing HasValue and Value. The null-conditional operator ?. short-circuits an entire member-access chain to null the moment it hits a null, instead of throwing a NullReferenceException. The null-coalescing operator ?? supplies a fallback value when the left-hand side is null, and ??= assigns a variable only if it is currently null.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Accessing .Value on a Nullable<T> that has no value throws an InvalidOperationException. Prefer GetValueOrDefault() or a null-coalescing fallback (maybeAge ?? 0) over .Value when you aren't certain it's populated."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "int.TryParse", ReturnType = "bool", Parameters = "string? s, out int result", Description = "Attempts to parse a string as an int, returning false instead of throwing when the text isn't a valid number." },
                new() { MethodName = "string.IsNullOrEmpty", ReturnType = "bool", Parameters = "string? value", Description = "Returns true when the string is null or has zero length — the standard guard before using a string." },
                new() { MethodName = "Nullable<T>.HasValue", ReturnType = "bool", Parameters = "", Description = "True when the nullable value type currently holds a value rather than null." },
                new() { MethodName = "Nullable<T>.GetValueOrDefault", ReturnType = "T", Parameters = "T defaultValue = default", Description = "Returns the contained value, or the supplied default (or default(T)) when the nullable is null — never throws." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_fundamentals_value_vs_reference",
                    Title = "Value Types vs Reference Types",
                    Description = "See the copy-on-assignment behavior of value types contrasted with the shared-reference behavior of reference types.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;

                    // Value type: assigning copies the data.
                    int a = 10;
                    int b = a;
                    b = 20;
                    Console.WriteLine($"a = {a}, b = {b}"); // a = 10, b = 20 (independent copies)

                    // Reference type: assigning copies the reference, not the object itself.
                    var listA = new List<int> { 1, 2, 3 };
                    var listB = listA;
                    listB.Add(4);
                    Console.WriteLine($"listA has {listA.Count} items"); // 4 - same underlying object!

                    // struct is a value type too, even though it looks like a class.
                    struct Point
                    {
                        public int X;
                        public int Y;
                    }

                    Point p1 = new Point { X = 1, Y = 2 };
                    Point p2 = p1;
                    p2.X = 99;
                    Console.WriteLine($"p1.X = {p1.X}, p2.X = {p2.X}"); // p1.X = 1, p2.X = 99
                    """
                },
                new()
                {
                    Id = "snip_learn_fundamentals_switch_patterns",
                    Title = "Switch Expressions & Pattern Matching",
                    Description = "Classify values of different types and ranges with a single switch expression, using type patterns and when guards.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    string Describe(object? value) => value switch
                    {
                        null => "nothing at all",
                        int n when n < 0 => $"a negative integer ({n})",
                        int n when n == 0 => "zero",
                        int n => $"a positive integer ({n})",
                        string s when string.IsNullOrEmpty(s) => "an empty string",
                        string s => $"a string of length {s.Length}",
                        double d => $"a double ({d})",
                        _ => $"something else: {value.GetType().Name}"
                    };

                    object?[] samples = { -5, 0, 42, "", "hello", 3.14, true, null };

                    foreach (var sample in samples)
                    {
                        Console.WriteLine($"{(sample ?? "null"),-8} => {Describe(sample)}");
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_fundamentals_null_operators",
                    Title = "null-Conditional, null-Coalescing & Nullable<T>",
                    Description = "Safely navigate possibly-null references with ?. and supply fallbacks with ?? and ??=.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;

                    class Address
                    {
                        public string? City { get; set; }
                    }

                    class Person
                    {
                        public string Name { get; set; } = "";
                        public Address? Address { get; set; }
                    }

                    var people = new List<Person>
                    {
                        new() { Name = "Ada", Address = new Address { City = "London" } },
                        new() { Name = "Grace", Address = null }
                    };

                    foreach (var person in people)
                    {
                        // ?. short-circuits to null instead of throwing when Address is null.
                        string? city = person.Address?.City;

                        // ?? supplies a fallback when the left side is null.
                        string displayCity = city ?? "Unknown city";

                        Console.WriteLine($"{person.Name} lives in {displayCity}");
                    }

                    // ??= assigns only if the variable is currently null.
                    string? nickname = null;
                    nickname ??= "Anonymous";
                    Console.WriteLine($"Nickname: {nickname}");

                    // Nullable<T> wraps a value type so it can represent "no value".
                    int? maybeAge = null;
                    Console.WriteLine($"HasValue: {maybeAge.HasValue}, GetValueOrDefault(18): {maybeAge.GetValueOrDefault(18)}");

                    maybeAge = 30;
                    Console.WriteLine($"HasValue: {maybeAge.HasValue}, Value: {maybeAge.Value}");
                    """
                }
            }
        };
    }
}
