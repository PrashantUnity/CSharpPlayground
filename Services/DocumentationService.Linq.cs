using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildLinqCategory()
    {
        return new DocCategory
        {
            Id = "linq",
            Title = "LINQ",
            IconKind = MaterialIconKind.FilterVariant,
            AccentColor = "#60A5FA",
            Badge = "Queries",
            Description = "Language Integrated Query: filtering, projecting, and aggregating data with a fluent, declarative syntax.",
            Articles = new List<DocArticle>
            {
                CreateLinqArticle()
            }
        };
    }

    private DocArticle CreateLinqArticle()
    {
        return new DocArticle
        {
            Id = "learn_linq",
            Title = "LINQ: Language Integrated Query",
            Subtitle = "Filter, project, and aggregate any sequence with the same fluent, declarative syntax.",
            ReadingTime = "8 min read",
            Summary = "LINQ turns loops full of if-statements and temporary lists into short, composable query expressions that read like what they do — and the same operators work whether the data is an in-memory list or, via a different provider, rows in a database.",
            Keywords = new List<string> { "linq", "select", "where", "groupby", "join", "aggregate", "orderby", "deferred execution", "ienumerable", "iqueryable", "tolist", "linq to objects", "entity framework" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "LINQ to Objects",
                    Content = "LINQ to Objects is LINQ run directly against in-memory collections — anything that implements IEnumerable<T>, such as List<T>, arrays, or the result of another query. Every operator is an extension method in System.Linq, so `using System.Linq;` is all it takes to unlock `.Where(...)`, `.Select(...)`, and the rest on any sequence."
                },
                new()
                {
                    Heading = "Select, Where, Join & GroupBy",
                    Content = "Where filters a sequence down to the elements matching a predicate. Select projects each element into a new shape (a different type, or just the fields you need). Join pairs up elements from two sequences on a matching key, much like a SQL inner join. GroupBy buckets elements that share a key into groups you can then aggregate."
                },
                new()
                {
                    Heading = "Projection & Aggregation",
                    Content = "Projection (Select / SelectMany) reshapes data without reducing how many items there are. Aggregation collapses a sequence down to a single value: Count, Sum, Average, Min, Max, and the general-purpose Aggregate, which folds a sequence left-to-right with an accumulator function you supply."
                },
                new()
                {
                    Heading = "Deferred vs Immediate Execution",
                    Content = "Most LINQ operators (Where, Select, OrderBy, GroupBy) are deferred — building a query just builds a description of the work; nothing actually runs until you enumerate it, such as with foreach, ToList(), or Count(). Operators that return a single value or a materialized collection (ToList, ToArray, ToDictionary, Count, First) force immediate execution right away.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Because a deferred query re-runs every time you enumerate it, if the source collection changes between enumerations, you'll see different results from the *same* query variable — call .ToList() at the point you want to \"freeze\" the results."
                },
                new()
                {
                    Heading = "IEnumerable vs IQueryable",
                    Content = "IEnumerable<T> queries (LINQ to Objects) execute as compiled C# — each operator runs in-process against objects already in memory. IQueryable<T> queries (LINQ to Entities, LINQ to SQL, and similar providers) instead build an expression tree that the provider translates into another language, typically SQL, and only sends to the database when you enumerate the result.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Against an IQueryable<T> (e.g. Entity Framework's DbSet<T>), keep your filtering (Where) and projection (Select) inside the query so they translate to SQL and run in the database — calling .ToList() (or .AsEnumerable()) too early pulls every row into memory first and finishes the rest of the query in the client with LINQ to Objects."
                },
                new()
                {
                    Heading = "Performance Considerations",
                    Content = "Chaining several Where/Select calls in a readable pipeline is usually fine — the compiler and runtime handle it efficiently. Watch for two common traps instead: re-enumerating the same deferred query multiple times (each enumeration re-runs the whole pipeline, so materialize once with ToList() if you need the results twice), and calling .Count() or .Any() after already materializing with ToList() when the un-materialized query would have answered the question just as well with less work."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Enumerable.Select", ReturnType = "IEnumerable<TResult>", Parameters = "Func<TSource, TResult> selector", Description = "Projects each element of a sequence into a new form." },
                new() { MethodName = "Enumerable.Where", ReturnType = "IEnumerable<TSource>", Parameters = "Func<TSource, bool> predicate", Description = "Filters a sequence to the elements that satisfy a condition." },
                new() { MethodName = "Enumerable.GroupBy", ReturnType = "IEnumerable<IGrouping<TKey, TSource>>", Parameters = "Func<TSource, TKey> keySelector", Description = "Groups the elements of a sequence according to a key selector function." },
                new() { MethodName = "Enumerable.Join", ReturnType = "IEnumerable<TResult>", Parameters = "IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector", Description = "Correlates elements of two sequences based on matching keys, like a SQL inner join." },
                new() { MethodName = "Enumerable.Aggregate", ReturnType = "TAccumulate", Parameters = "TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func", Description = "Applies an accumulator function over a sequence, folding it down to a single value." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_linq_pipeline",
                    Title = "A Select / Where / OrderBy Pipeline",
                    Description = "Filter, project, and sort a list of records in one fluent, readable chain.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;
                    using System.Linq;

                    var products = new List<Product>
                    {
                        new("Wireless Mouse", 24.99m, 40),
                        new("Mechanical Keyboard", 89.99m, 0),
                        new("USB-C Hub", 34.50m, 15),
                        new("Monitor Stand", 45.00m, 8),
                        new("Webcam", 59.99m, 0)
                    };

                    var inStockSummaries = products
                        .Where(p => p.StockCount > 0)
                        .OrderBy(p => p.Price)
                        .Select(p => $"{p.Name} - ${p.Price} ({p.StockCount} in stock)")
                        .ToList();

                    foreach (string summary in inStockSummaries)
                    {
                        Console.WriteLine(summary);
                    }

                    record Product(string Name, decimal Price, int StockCount);
                    """
                },
                new()
                {
                    Id = "snip_learn_linq_groupby",
                    Title = "GroupBy with Aggregation",
                    Description = "Group orders by customer and compute a total and count per group.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;
                    using System.Linq;

                    var orders = new List<Order>
                    {
                        new("Ada", 120.00m),
                        new("Grace", 75.50m),
                        new("Ada", 40.25m),
                        new("Alan", 200.00m),
                        new("Grace", 15.00m)
                    };

                    var summaryByCustomer = orders
                        .GroupBy(o => o.Customer)
                        .Select(group => new
                        {
                            Customer = group.Key,
                            OrderCount = group.Count(),
                            Total = group.Sum(o => o.Amount)
                        })
                        .OrderByDescending(s => s.Total);

                    foreach (var summary in summaryByCustomer)
                    {
                        Console.WriteLine($"{summary.Customer}: {summary.OrderCount} orders, ${summary.Total} total");
                    }

                    record Order(string Customer, decimal Amount);
                    """
                },
                new()
                {
                    Id = "snip_learn_linq_deferred",
                    Title = "Deferred vs Immediate Execution",
                    Description = "Watch a deferred query pick up a change to its source, then freeze the results with ToList().",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;
                    using System.Linq;

                    var numbers = new List<int> { 1, 2, 3 };

                    // Deferred: this line does NOT run the query yet, it just describes it.
                    IEnumerable<int> evenNumbersQuery = numbers.Where(n => n % 2 == 0);

                    numbers.Add(4);
                    numbers.Add(6);

                    Console.WriteLine("Deferred query result (sees the added numbers):");
                    foreach (int n in evenNumbersQuery)
                    {
                        Console.WriteLine($" - {n}");
                    }

                    // Immediate: ToList() enumerates right now and freezes a snapshot.
                    List<int> evenNumbersSnapshot = numbers.Where(n => n % 2 == 0).ToList();
                    numbers.Add(8);

                    Console.WriteLine("Immediate snapshot (does NOT see the later addition):");
                    foreach (int n in evenNumbersSnapshot)
                    {
                        Console.WriteLine($" - {n}");
                    }
                    """
                }
            }
        };
    }
}
