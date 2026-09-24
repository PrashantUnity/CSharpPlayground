using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildAdvancedTopicsCategory()
    {
        return new DocCategory
        {
            Id = "advanced_topics",
            Title = "Bonus: Advanced Topics",
            IconKind = MaterialIconKind.RocketLaunchOutline,
            AccentColor = "#E879F9",
            Badge = "Advanced",
            Description = "Performance-oriented and advanced C#/.NET topics, plus a recommended learning path through this curriculum.",
            Articles = new List<DocArticle>
            {
                CreateAdvancedTopicsArticle()
            }
        };
    }

    private DocArticle CreateAdvancedTopicsArticle()
    {
        return new DocArticle
        {
            Id = "learn_advanced_topics",
            Title = "Advanced Topics: Span<T>, Performance & Where to Go Next",
            Subtitle = "Peek at the low-allocation, high-performance corner of .NET, the architectures beyond a single process, and a suggested order to work through this whole curriculum.",
            ReadingTime = "9 min read",
            Summary = "This bonus chapter rounds out the curriculum with performance-oriented C# features (Span<T>, Memory<T>, unsafe code), a nod to the Roslyn compiler this very app is built on, a look at scaling out beyond a single process (microservices, gRPC, Docker), and a recommended order to work through the whole set of documentation categories.",
            Keywords = new List<string> { "span", "memory", "unsafe", "roslyn", "performance", "stringbuilder", "microservices", "grpc", "docker", "advanced", "learning path", "stackalloc" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Span<T> & Memory<T>",
                    Content = "Span<T> is a stack-only, ref-struct view over a contiguous block of memory — an array, a slice of an array, or a stack-allocated buffer — without copying it. Slicing a Span (`span.Slice(start, length)`, or the `span[start..end]` range syntax) creates another view over the same memory, so parsing and splitting operations can avoid allocating new arrays or substrings entirely.",
                    BulletPoints = new List<string>
                    {
                        "Span<T> can point at stack memory (via stackalloc), so it cannot be stored in a field, boxed, or captured by a lambda/async method.",
                        "Memory<T> is Span<T>'s heap-friendly cousin — it can live in a field or cross an await, and you get a Span from it with `.Span` when you're ready to work with it synchronously.",
                        "string.AsSpan() gives you a Span<char> view over a string without copying it — great for parsing without substring allocations."
                    },
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Reach for Span<T> when profiling shows allocation pressure from substrings, array slices, or buffer copies in a hot path — it's a performance tool, not a everyday replacement for arrays and strings."
                },
                new()
                {
                    Heading = "Unsafe Code",
                    Content = "C# lets you opt into raw pointer arithmetic inside an `unsafe` context, compiled with `AllowUnsafeBlocks` enabled. The `fixed` statement pins a managed object (like an array) so the garbage collector won't move it while you hold a pointer into it, since the GC is otherwise free to relocate objects during a compaction.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Unsafe code disables the runtime's memory-safety guarantees for that block — an out-of-bounds pointer write can corrupt memory silently. Reach for Span<T> first; drop to unsafe/pointers only when profiling proves you need it."
                },
                new()
                {
                    Heading = "Roslyn Compiler",
                    Content = "Roslyn is Microsoft's open-source C#/VB compiler platform — it doesn't just turn source into IL, it exposes that entire process (syntax trees, semantic analysis, emitted assemblies) as an API. Roslyn is what powers live features like IntelliSense and analyzers in the IDE, and it's also what makes C# scripting possible: compiling and running a snippet of code on the fly, with no separate build step.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Fun aside: this very app — C# Code Studio — uses Roslyn's scripting APIs (Microsoft.CodeAnalysis.CSharp.Scripting) under the hood to compile and execute the code you write in its editor, on a background thread so the UI stays responsive."
                },
                new()
                {
                    Heading = "Performance Optimization",
                    Content = "Two of the cheapest, highest-leverage performance habits in everyday C# are avoiding unnecessary allocations and avoiding unnecessary copying. String concatenation in a loop (`result += piece`) allocates a brand-new string on every iteration because strings are immutable; StringBuilder instead grows an internal buffer, turning an O(n²) pattern into an O(n) one for large inputs.",
                    BulletPoints = new List<string>
                    {
                        "Measure before optimizing — Stopwatch (or a proper benchmarking tool like BenchmarkDotNet) tells you where time actually goes, rather than guessing.",
                        "Prefer StringBuilder over += concatenation whenever the number of pieces isn't small and fixed.",
                        "Struct-based, allocation-free types like Span<T> and ValueTuple reduce garbage-collector pressure in hot paths."
                    }
                },
                new()
                {
                    Heading = "Microservices",
                    Content = "A microservices architecture splits an application into a set of small, independently deployable services, each typically owning its own data and communicating with the others over the network (HTTP APIs, message queues, or RPC). This trades the simplicity of a single deployable (a \"monolith\") for independent scaling, deployment, and technology choices per service — at the cost of distributed-systems complexity: network failures, eventual consistency, and the need for service discovery, observability, and resilient communication patterns (retries, circuit breakers).",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "This section is conceptual — there's no single runnable snippet for an architectural style. ASP.NET Core (covered earlier in this curriculum) is the typical building block each individual microservice is built from."
                },
                new()
                {
                    Heading = "gRPC",
                    Content = "gRPC is a high-performance RPC framework built on HTTP/2 that lets you define a service contract once, in a .proto file, and generate strongly-typed client and server code for it — including for .NET, via the Grpc.AspNetCore package. Compared to a JSON-over-HTTP API, gRPC's binary Protocol Buffers format and support for bidirectional streaming make it a common choice for service-to-service communication inside a microservices architecture, where low latency and typed contracts matter more than human-readability.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Conceptual section — running gRPC requires the Grpc.AspNetCore/Grpc.Net.Client packages, a .proto contract, and generated code, so there's no standalone snippet to run here."
                },
                new()
                {
                    Heading = "Docker with .NET",
                    Content = "Docker packages an application together with its runtime and dependencies into a portable, isolated container image, so it runs the same way on a developer's laptop, a CI server, or a production host. .NET has first-class support for this: `dotnet publish` combined with the official Microsoft base images (e.g. mcr.microsoft.com/dotnet/aspnet for running, mcr.microsoft.com/dotnet/sdk for building) produces small, layered images, and `dotnet publish /t:PublishContainer` can build an image directly without even hand-writing a Dockerfile.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Conceptual section — building and running a container requires Docker itself installed on the host machine, so there's no standalone C# snippet to run here."
                },
                new()
                {
                    Heading = "Recommended Learning Path",
                    Content = "Having covered everything from language fundamentals through architecture, here is a suggested order to work through this documentation set end-to-end, roughly following how each topic builds on the last.",
                    CalloutType = DocCalloutType.Tip,
                    BulletPoints = new List<string>
                    {
                        "1. C# Fundamentals",
                        "2. OOP & Memory Management",
                        "3. Collections & LINQ",
                        "4. Delegates & Events",
                        "5. Async Programming",
                        "6. ASP.NET Core",
                        "7. EF Core",
                        "8. Design Patterns",
                        "9. Testing & Clean Architecture"
                    }
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Span<T>.Slice", ReturnType = "Span<T>", Parameters = "int start, int length", Description = "Returns a new Span over a sub-range of the same underlying memory — no copy is made." },
                new() { MethodName = "ReadOnlySpan<char> (via string.AsSpan)", ReturnType = "ReadOnlySpan<char>", Parameters = "", Description = "A zero-allocation, read-only view over a string's characters, usable with slicing, range indexers, and most string-parsing APIs." },
                new() { MethodName = "Memory<T>.Span", ReturnType = "Span<T>", Parameters = "", Description = "Projects a heap-friendly Memory<T> (which can be stored in a field or cross an await) into a Span<T> for synchronous, allocation-free access." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_advanced_span_slicing",
                    Title = "Span<T> — Parsing a String Without Allocating",
                    Description = "Runs directly in this app's script runner: splits a \"key=value\" string into its two parts using ReadOnlySpan<char> slices, without allocating any substrings.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    ReadOnlySpan<char> line = "username=frypdf_user".AsSpan();

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex < 0)
                    {
                        Console.WriteLine("No '=' found.");
                        return;
                    }

                    ReadOnlySpan<char> key = line[..separatorIndex];       // slice, no allocation
                    ReadOnlySpan<char> value = line[(separatorIndex + 1)..]; // slice, no allocation

                    Console.WriteLine($"Key:   {key.ToString()}");
                    Console.WriteLine($"Value: {value.ToString()}");

                    // A small stack-allocated buffer, sliced the same way — never touches the managed heap.
                    Span<int> buffer = stackalloc int[5] { 10, 20, 30, 40, 50 };
                    Span<int> middle = buffer.Slice(1, 3);

                    Console.WriteLine($"Middle slice: [{string.Join(", ", middle.ToArray())}]");
                    """
                },
                new()
                {
                    Id = "snip_learn_advanced_stringbuilder_perf",
                    Title = "Performance: StringBuilder vs String Concatenation",
                    Description = "Runs directly in this app's script runner: times naive string concatenation against StringBuilder for the same amount of work, and prints both durations for comparison.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Diagnostics;
                    using System.Text;

                    const int iterations = 20_000;

                    var concatStopwatch = Stopwatch.StartNew();
                    string concatenated = string.Empty;
                    for (int i = 0; i < iterations; i++)
                    {
                        concatenated += i; // allocates a brand-new string on every single iteration
                    }
                    concatStopwatch.Stop();

                    var builderStopwatch = Stopwatch.StartNew();
                    var builder = new StringBuilder();
                    for (int i = 0; i < iterations; i++)
                    {
                        builder.Append(i); // grows one internal buffer instead of reallocating a string each time
                    }
                    string built = builder.ToString();
                    builderStopwatch.Stop();

                    Console.WriteLine($"String concatenation: {concatStopwatch.ElapsedMilliseconds} ms ({concatenated.Length:N0} chars)");
                    Console.WriteLine($"StringBuilder:        {builderStopwatch.ElapsedMilliseconds} ms ({built.Length:N0} chars)");
                    Console.WriteLine(concatenated == built
                        ? "Both approaches produced identical output — StringBuilder just got there with far fewer allocations."
                        : "Unexpected mismatch between the two results.");
                    """
                }
            }
        };
    }
}
