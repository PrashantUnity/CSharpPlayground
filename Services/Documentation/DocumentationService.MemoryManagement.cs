using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildMemoryManagementCategory()
    {
        return new DocCategory
        {
            Id = "memory_management",
            Title = "Memory Management & Type System",
            IconKind = MaterialIconKind.Memory,
            AccentColor = "#F472B6",
            Badge = "Runtime",
            Description = "Stack vs heap, garbage collection, disposal, and C#'s type system.",
            Articles = new List<DocArticle>
            {
                CreateMemoryManagementArticle()
            }
        };
    }

    private DocArticle CreateMemoryManagementArticle()
    {
        return new DocArticle
        {
            Id = "learn_memory_management",
            Title = "Memory Management & the C# Type System",
            Subtitle = "Where your data actually lives, how the garbage collector reclaims it, and how to release unmanaged resources deterministically.",
            ReadingTime = "8 min read",
            Summary = "C# hides most memory management behind a garbage collector, but understanding stack vs heap, when GC runs, IDisposable, and the cost of boxing lets you write code that's both correct and fast. This chapter also compares class, struct, and record — three ways to shape data with very different memory and identity semantics.",
            Keywords = new List<string> { "stack", "heap", "garbage collection", "gc", "idisposable", "using", "using var", "record", "class vs struct", "immutability", "with expression", "boxing", "unboxing" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Stack vs Heap",
                    Content = "The stack is a small, fast region of memory that holds local variables and method call frames; it's managed automatically by simply popping off the top when a method returns, which is why stack allocation is essentially free. The heap is a much larger pool used for objects whose lifetime isn't tied to a single method call — every class instance lives here. A local value type lives on the stack, but a value type stored as a field of a class instance lives on the heap as part of that object.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "'Value types go on the stack' is a common simplification, not a hard rule — a struct field inside a class, a boxed struct, or a struct captured by a lambda closure all end up on the heap. What's actually guaranteed is copy-by-value semantics, not a specific memory location."
                },
                new()
                {
                    Heading = "Value Types vs Reference Types",
                    Content = "This distinction (introduced in the Fundamentals article) is central to memory management too: a reference-type variable is really just a pointer to a heap-allocated object, plus a small amount of object header overhead (type pointer, sync block). Passing a large struct around by value repeatedly copies its full contents, which can hurt performance — passing it with in or ref avoids the copy when the method doesn't need (or shouldn't have) its own copy.",
                    BulletPoints = new List<string>
                    {
                        "A large struct copied frequently can be slower than a class reference — measure before assuming a struct is 'free'.",
                        "A small, immutable struct (a Point, a Money value) is usually the sweet spot for value-type design.",
                        "Reference types support null; struct value types do not, unless wrapped in Nullable<T>."
                    }
                },
                new()
                {
                    Heading = "Garbage Collection (GC)",
                    Content = ".NET's garbage collector automatically reclaims heap memory occupied by objects that are no longer reachable from any root (a local variable, a static field, a CPU register). It organizes the heap into generations — Gen 0 for new, short-lived objects, Gen 1 as a buffer, and Gen 2 for long-lived objects — because collecting Gen 0 frequently is cheap and most objects die young. You almost never need to interact with the GC directly; it runs automatically when memory pressure warrants it.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Calling GC.Collect() manually is almost always a mistake in application code — it forces a full, expensive collection and defeats the generational tuning the runtime already does for you. It exists mainly for very specific scenarios (post-startup cleanup, memory-constrained diagnostics, unit tests verifying finalizers) — not as a routine performance fix."
                },
                new()
                {
                    Heading = "IDisposable & using",
                    Content = "The garbage collector only knows about managed memory — it has no idea that an object is holding a file handle, a network socket, or a database connection. IDisposable's Dispose() method is the deterministic, explicit way to release that kind of unmanaged resource the moment you're done with it, rather than waiting for an unpredictable future GC pass. The using statement (or the more concise using var declaration) guarantees Dispose() runs even if an exception is thrown inside the block.",
                    BulletPoints = new List<string>
                    {
                        "using (var stream = File.OpenRead(path)) { ... } — Dispose() runs at the closing brace.",
                        "using var stream = File.OpenRead(path); — Dispose() runs at the end of the enclosing scope (C# 8+), no extra nesting needed.",
                        "Implement IDisposable yourself whenever a class owns another IDisposable (a stream, an HttpClient field) so callers can dispose the wrapper and have it cascade."
                    }
                },
                new()
                {
                    Heading = "record vs class vs struct",
                    Content = "class gives you a reference type with identity-based equality by default (two instances are equal only if they're the same object). record (a reference type, unless declared record struct) gives you value-based equality out of the box — two records with identical property values are equal — plus a compiler-generated ToString() and a with expression for non-destructive mutation. record is purpose-built for immutable data models (DTOs, API payloads, domain value objects) where 'same data' should mean 'equal', which class does not give you for free.",
                    BulletPoints = new List<string>
                    {
                        "class — reference type, mutable by default, reference equality unless you override Equals/GetHashCode yourself.",
                        "record — reference type (by default), value equality and ToString() generated automatically, encourages immutability via init-only properties.",
                        "struct / record struct — value types, copied on assignment; record struct adds the same value-equality/ToString() generation to a struct."
                    }
                },
                new()
                {
                    Heading = "Immutability",
                    Content = "An immutable object cannot be changed after construction — every 'modification' instead produces a new object, leaving the original untouched. This eliminates whole categories of bugs around shared mutable state (especially across threads) because an immutable object can be freely passed around and cached without anyone needing to defensively copy it. record's init-only properties and with expression make immutability the path of least resistance: with produces a shallow copy with only the specified properties changed.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Favor init instead of set on record and class properties whenever an object represents a fixed fact (an event that already happened, a completed order) rather than something with an evolving lifecycle."
                },
                new()
                {
                    Heading = "Boxing & Unboxing",
                    Content = "Boxing is the implicit conversion of a value type to object (or to an interface it implements), which allocates a new object on the heap and copies the value into it. Unboxing is the reverse: extracting the value type back out of the boxed object, which requires an explicit cast and throws InvalidCastException if the boxed type doesn't match exactly. Both directions cost a heap allocation (boxing) or a runtime type check and copy (unboxing), which matters in hot loops or when storing many value types in a non-generic collection like ArrayList.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Generic collections (List<int> instead of ArrayList) avoid boxing entirely because the element type is fixed at compile time — this is one of the main performance reasons generics replaced non-generic collections in modern C#."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "GC.Collect", ReturnType = "void", Parameters = "", Description = "Forces an immediate garbage collection. Rarely appropriate in application code — the runtime already schedules collections based on memory pressure." },
                new() { MethodName = "IDisposable.Dispose", ReturnType = "void", Parameters = "", Description = "Releases unmanaged resources deterministically. Called automatically at the end of a using block or using var scope." },
                new() { MethodName = "GC.SuppressFinalize", ReturnType = "void", Parameters = "object obj", Description = "Tells the GC to skip running an object's finalizer, since Dispose() already released its resources — used inside a correct IDisposable implementation to avoid redundant finalization work." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_memory_idisposable_using",
                    Title = "IDisposable & using var",
                    Description = "Implement IDisposable on a class that owns an unmanaged-style resource, and release it deterministically with using var.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    class ResourceHandle : IDisposable
                    {
                        private bool _disposed;
                        public string Name { get; }

                        public ResourceHandle(string name)
                        {
                            Name = name;
                            Console.WriteLine($"[{Name}] acquired");
                        }

                        public void DoWork()
                        {
                            if (_disposed) throw new ObjectDisposedException(Name);
                            Console.WriteLine($"[{Name}] doing work");
                        }

                        public void Dispose()
                        {
                            if (_disposed) return;
                            Console.WriteLine($"[{Name}] released");
                            _disposed = true;
                            GC.SuppressFinalize(this);
                        }
                    }

                    // using var: Dispose() runs automatically at the end of this scope,
                    // even if an exception is thrown in between.
                    using (var handleA = new ResourceHandle("A"))
                    {
                        handleA.DoWork();
                    } // handleA.Dispose() runs here

                    using var handleB = new ResourceHandle("B");
                    handleB.DoWork();
                    Console.WriteLine("End of script — handleB.Dispose() runs automatically after this line.");
                    """
                },
                new()
                {
                    Id = "snip_learn_memory_record_immutability",
                    Title = "record Immutability & the with Expression",
                    Description = "Compare value-based equality on a record against reference equality on an equivalent class, then create a modified copy with with.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    record Point3D(double X, double Y, double Z);

                    class MutablePoint3D
                    {
                        public double X { get; set; }
                        public double Y { get; set; }
                        public double Z { get; set; }
                    }

                    var recordA = new Point3D(1, 2, 3);
                    var recordB = new Point3D(1, 2, 3);
                    Console.WriteLine($"records equal by value: {recordA == recordB}"); // True
                    Console.WriteLine(recordA); // auto-generated ToString(): Point3D { X = 1, Y = 2, Z = 3 }

                    var classA = new MutablePoint3D { X = 1, Y = 2, Z = 3 };
                    var classB = new MutablePoint3D { X = 1, Y = 2, Z = 3 };
                    Console.WriteLine($"classes equal by reference: {classA == classB}"); // False, different objects

                    // with produces a new record, leaving the original untouched.
                    var movedPoint = recordA with { Z = 10 };
                    Console.WriteLine($"original: {recordA}");
                    Console.WriteLine($"moved:    {movedPoint}");
                    """
                },
                new()
                {
                    Id = "snip_learn_memory_boxing_unboxing",
                    Title = "Boxing & Unboxing Cost",
                    Description = "Measure the allocation cost of boxing value types into a non-generic collection compared to a generic collection that never boxes.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections;
                    using System.Collections.Generic;
                    using System.Diagnostics;

                    const int count = 500_000;

                    // ArrayList stores object references, so every int added is boxed onto the heap.
                    var boxedStopwatch = Stopwatch.StartNew();
                    var arrayList = new ArrayList();
                    for (int i = 0; i < count; i++)
                    {
                        arrayList.Add(i); // boxing: int -> object allocation
                    }
                    long boxedSum = 0;
                    foreach (object boxed in arrayList)
                    {
                        boxedSum += (int)boxed; // unboxing: object -> int
                    }
                    boxedStopwatch.Stop();

                    // List<int> is generic, so the int is stored directly — no boxing at all.
                    var genericStopwatch = Stopwatch.StartNew();
                    var typedList = new List<int>();
                    for (int i = 0; i < count; i++)
                    {
                        typedList.Add(i);
                    }
                    long typedSum = 0;
                    foreach (int value in typedList)
                    {
                        typedSum += value;
                    }
                    genericStopwatch.Stop();

                    Console.WriteLine($"ArrayList (boxed):  {boxedStopwatch.ElapsedMilliseconds,4} ms, sum = {boxedSum}");
                    Console.WriteLine($"List<int> (typed):  {genericStopwatch.ElapsedMilliseconds,4} ms, sum = {typedSum}");
                    Console.WriteLine("The generic list avoids a heap allocation per element that the boxed ArrayList pays for.");
                    """
                }
            }
        };
    }
}
