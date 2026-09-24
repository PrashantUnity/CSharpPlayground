using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildDotNetRuntimeCategory()
    {
        return new DocCategory
        {
            Id = "dotnet_runtime",
            Title = ".NET Runtime & Ecosystem",
            IconKind = MaterialIconKind.Cogs,
            AccentColor = "#94A3B8",
            Badge = "Platform",
            Description = "How .NET compiles and runs your code: the CLR, assemblies, JIT, and the SDK/NuGet ecosystem.",
            Articles = new List<DocArticle>
            {
                CreateDotNetRuntimeArticle()
            }
        };
    }

    private DocArticle CreateDotNetRuntimeArticle()
    {
        return new DocArticle
        {
            Id = "learn_dotnet_runtime",
            Title = "What Actually Runs Your Code: CLR, Assemblies & the SDK",
            Subtitle = "The platform underneath every C# program — from source text to a running process.",
            ReadingTime = "6 min read",
            Summary = "Writing C# is easy to treat as a black box: you write code, you press run, it works. This chapter opens the box a little — what the CLR actually does, how CTS/CLS let different .NET languages interoperate, what an assembly is, why the JIT exists, and how the SDK, runtime, and NuGet fit together.",
            Keywords = new List<string> { "clr", "common language runtime", "cts", "cls", "common type system", "assembly", "dll", "jit", "just-in-time compilation", "nuget", "sdk vs runtime", "il", "intermediate language" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "The CLR (Common Language Runtime)",
                    Content = "The CLR is the engine that actually executes .NET programs. When you build a C# project, the compiler doesn't produce native machine code directly — it produces Intermediate Language (IL), a CPU-independent instruction set, packaged into an assembly. The CLR loads that assembly, manages memory for it (allocating objects, running the garbage collector to reclaim ones no longer reachable), enforces type safety and security boundaries, and hands execution over to the JIT compiler to turn IL into native code the CPU can actually run.",
                    BulletPoints = new List<string>
                    {
                        "Garbage collection — the CLR tracks object lifetimes and frees memory automatically; you don't call free() or delete.",
                        "Type safety — the CLR verifies IL doesn't do unsafe things like treating an int as a pointer.",
                        "Exception handling, threading primitives, and interop with native code are all services the CLR provides, not the language."
                    }
                },
                new()
                {
                    Heading = "CTS & CLS: Why C#, F#, and VB.NET Can Share a DLL",
                    Content = "The Common Type System (CTS) defines the set of types (int, string, classes, interfaces, delegates, and so on) that every .NET language ultimately compiles down to, so a class written in C# and one written in F# describe themselves to the runtime in exactly the same terms. The Common Language Specification (CLS) is a narrower subset of CTS — a set of rules a public API can follow to guarantee it's usable from any CLS-compliant language, even one with different casing or numeric-type rules than C#.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "This is why a NuGet package written in F# can be referenced directly from a C# project with full IntelliSense — both compile to the same CTS-described IL, and a CLS-compliant public API reads the same from either language."
                },
                new()
                {
                    Heading = "Assemblies & DLLs",
                    Content = "An assembly is the unit of deployment and versioning in .NET: a .dll (library) or .exe (application) file containing compiled IL, type metadata describing everything in it, and a manifest listing its own identity and the other assemblies it depends on. Metadata is what makes reflection possible — code can ask an assembly at runtime what types it contains, what members they expose, and what attributes decorate them, without a separate header file.",
                    BulletPoints = new List<string>
                    {
                        "A single project typically compiles to one assembly, but one assembly can contain many namespaces and types.",
                        "Assembly metadata is also what powers features like Assembly.GetExecutingAssembly() and Type.GetType(...) at runtime.",
                        "Strong naming and versioning info in the manifest let the runtime resolve which exact version of a dependency to load."
                    }
                },
                new()
                {
                    Heading = "JIT Compilation",
                    Content = "IL isn't native machine code, so it can't run directly on the CPU. The Just-In-Time (JIT) compiler translates each method's IL into native code the first time that method is actually called, then caches the result so subsequent calls skip recompilation. This happens per-process, tailored to the exact CPU the code is running on — one reason .NET IL is portable across architectures while the compiled output is fully optimized for whichever machine it ends up on.",
                    BulletPoints = new List<string>
                    {
                        "Just-in-time means \"on first use,\" not \"ahead of time\" — this is the opposite tradeoff from a fully precompiled native binary.",
                        "ReadyToRun and Native AOT are alternative, ahead-of-time compilation modes .NET also offers, trading some flexibility for faster startup.",
                        "The JIT can apply optimizations a static compiler cannot, such as specializing hot code paths based on runtime behavior."
                    }
                },
                new()
                {
                    Heading = "NuGet Packages",
                    Content = "NuGet is .NET's package manager: a NuGet package is essentially a zip file containing one or more assemblies (often built for several target frameworks) plus metadata about its own dependencies. Referencing a package in a project file pulls in that assembly and everything it in turn depends on, resolved transitively by the SDK's build tooling — no separate manual download-and-reference step required, unlike this project's own dependency-free code snippets."
                },
                new()
                {
                    Heading = "SDK vs Runtime",
                    Content = "The .NET Runtime is only what's needed to run an already-built application: the CLR, the base class libraries, and the JIT. The .NET SDK is a superset that adds everything needed to build one: the C# compiler (Roslyn), the dotnet CLI's build/publish/test/pack commands, and project templates. A deployment machine typically only needs the runtime installed; a development machine (or this Code Studio's own build pipeline) needs the full SDK.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "\"Works on my machine but not on the server\" is very often exactly this distinction — a server with only the runtime installed can run a published app, but cannot build one, and may be missing a runtime version the app targets."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Assembly.GetExecutingAssembly", ReturnType = "Assembly", Parameters = "", Description = "Returns the Assembly object for the assembly containing the code that's currently running — useful for reading its own version, location, or metadata." },
                new() { MethodName = "Environment.Version", ReturnType = "Version", Parameters = "", Description = "Returns the version of the CLR that's currently hosting the process." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_dotnet_runtime_inspect_assembly",
                    Title = "Inspecting the Running Assembly & CLR Version",
                    Description = "Use reflection to look at the assembly your own code is running in, and the CLR version hosting it.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Reflection;

                    Assembly assembly = Assembly.GetExecutingAssembly();

                    Console.WriteLine($"Assembly full name: {assembly.FullName}");
                    Console.WriteLine($"CLR version hosting this process: {Environment.Version}");
                    Console.WriteLine($"OS platform: {Environment.OSVersion}");
                    Console.WriteLine($"Is 64-bit process: {Environment.Is64BitProcess}");

                    // A loaded assembly's metadata also lets you enumerate the types it defines —
                    // this is the same mechanism reflection-based tools (serializers, DI containers) rely on.
                    foreach (Type type in assembly.GetTypes())
                    {
                        Console.WriteLine($"  Type in this assembly: {type.FullName}");
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_dotnet_runtime_source_to_il_concept",
                    Title = "From Source to IL to Native Code (Conceptually)",
                    Description = "A comment-annotated walkthrough of the stages a method passes through before it actually executes. No special tooling required — this is explanatory, not a live disassembly.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    // 1. SOURCE CODE (what you write):
                    static int AddOne(int value)
                    {
                        return value + 1;
                    }

                    // 2. COMPILE TIME: the C# compiler (Roslyn) translates the method above into
                    //    Intermediate Language, roughly equivalent to this pseudo-IL:
                    //
                    //      .method static int32 AddOne(int32 value)
                    //      {
                    //          ldarg.0      // load argument 'value' onto the evaluation stack
                    //          ldc.i4.1     // load the constant 1
                    //          add          // pop both, push their sum
                    //          ret          // return the top of the stack
                    //      }
                    //
                    //    This IL, plus metadata describing the method's signature, is what actually
                    //    gets written into the compiled assembly (.dll) — not native CPU instructions.

                    // 3. FIRST CALL AT RUNTIME: the CLR's JIT compiler sees AddOne has no native code yet,
                    //    compiles that IL into machine code for the actual CPU this process is running on,
                    //    and caches it against the method.

                    // 4. EVERY SUBSEQUENT CALL: the CLR calls straight into the cached native code —
                    //    no more translation needed for the lifetime of the process.

                    Console.WriteLine($"AddOne(41) = {AddOne(41)}");
                    Console.WriteLine("The call above went through exactly the four stages described.");
                    """
                }
            }
        };
    }
}
