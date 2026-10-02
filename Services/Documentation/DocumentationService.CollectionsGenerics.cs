using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildCollectionsGenericsCategory()
    {
        return new DocCategory
        {
            Id = "collections_generics",
            Title = "Collections & Generics",
            IconKind = MaterialIconKind.FormatListBulleted,
            AccentColor = "#FBBF24",
            Badge = "Data",
            Description = "Built-in collection types, generic type parameters, and building your own collections.",
            Articles = new List<DocArticle>
            {
                CreateCollectionsGenericsArticle()
            }
        };
    }

    private DocArticle CreateCollectionsGenericsArticle()
    {
        return new DocArticle
        {
            Id = "learn_collections_generics",
            Title = "Collections & Generics",
            Subtitle = "Choose the right built-in collection, and write generic code that works across types safely.",
            ReadingTime = "7 min read",
            Summary = "The Base Class Library ships a collection type for almost every access pattern — ordered lists, fast lookups, unique sets, and FIFO/LIFO queues — and generics let your own methods and types work with any of them without giving up compile-time type safety.",
            Keywords = new List<string> { "list", "dictionary", "hashset", "queue", "stack", "generics", "generic constraint", "ienumerable", "icollection", "ilist", "custom collection", "iequatable", "icomparable" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "List<T> & Dictionary<TKey, TValue>",
                    Content = "List<T> is a resizable, index-ordered array — the default choice when you need an ordered sequence you can grow, shrink, sort, or index into. Dictionary<TKey, TValue> stores key/value pairs in a hash table, giving near O(1) lookup, insertion, and removal by key instead of the O(n) scan a List would need to find an item."
                },
                new()
                {
                    Heading = "HashSet<T>, Queue<T> & Stack<T>",
                    Content = "HashSet<T> stores only unique values with the same near O(1) hashing Dictionary uses, and adds set algebra like UnionWith, IntersectWith, and ExceptWith. Queue<T> is first-in-first-out (Enqueue/Dequeue) — natural for work items processed in arrival order. Stack<T> is last-in-first-out (Push/Pop) — natural for undo history or depth-first traversal.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Reach for HashSet<T> the moment you catch yourself calling list.Contains(x) in a loop — Contains on a List is a linear scan, while HashSet.Contains is a hash lookup."
                },
                new()
                {
                    Heading = "IEnumerable, ICollection & IList",
                    Content = "These interfaces form a ladder of capability. IEnumerable<T> only promises you can iterate it once, forward, with foreach — it's the interface to accept as a method parameter when all you do is read. ICollection<T> adds Count, Add, Remove, and Contains. IList<T> adds index-based access (this[int]) and Insert/RemoveAt. Coding against the narrowest interface that does the job keeps your methods reusable across List, array, HashSet, and more.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Arrays implement IList<T> but throw NotSupportedException from Add and Remove because their size is fixed — check for that if you accept IList<T> and intend to mutate it."
                },
                new()
                {
                    Heading = "Generics & Constraints",
                    Content = "A generic type parameter like <T> lets a class or method work with any type while the compiler still enforces type safety — no casting, no boxing of value types, and mismatched-type bugs caught at compile time instead of runtime. Constraints (where T : ...) narrow what T is allowed to be so the compiler lets you call members that aren't on plain object, such as where T : IComparable<T> to enable CompareTo, or where T : class / where T : struct to require a reference or value type, or where T : new() to require a public parameterless constructor."
                },
                new()
                {
                    Heading = "Custom Collections",
                    Content = "To make your own type usable in a foreach loop and with LINQ, implement IEnumerable<T> by returning an IEnumerator<T> — the easiest way is to write the iterator body with a `yield return` method rather than hand-writing an enumerator class. Implement ICollection<T> as well if callers also need Add, Remove, and Count.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "A type that implements only the non-generic IEnumerable (not IEnumerable<T>) forces every consumer to cast each element out of object — always implement the generic version for a strongly-typed collection."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "List<T>.Add", ReturnType = "void", Parameters = "T item", Description = "Appends an item to the end of the list, growing its internal array if needed." },
                new() { MethodName = "Dictionary<TKey, TValue>.TryGetValue", ReturnType = "bool", Parameters = "TKey key, out TValue value", Description = "Looks up a key without throwing if it's missing — returns false and sets value to default instead." },
                new() { MethodName = "IEnumerable<T>.GetEnumerator", ReturnType = "IEnumerator<T>", Parameters = "", Description = "Returns an enumerator that iterates the collection once, forward — what a foreach loop calls under the hood." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_collections_overview",
                    Title = "List, Dictionary, HashSet, Queue & Stack Side by Side",
                    Description = "The same small dataset stored in five different collections, each suited to a different access pattern.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;

                    var names = new List<string> { "Ada", "Grace", "Alan" };
                    names.Add("Katherine");
                    Console.WriteLine($"List (ordered, indexable): {string.Join(", ", names)}, first = {names[0]}");

                    var ages = new Dictionary<string, int>
                    {
                        ["Ada"] = 36,
                        ["Grace"] = 85,
                        ["Alan"] = 41
                    };
                    if (ages.TryGetValue("Grace", out int graceAge))
                    {
                        Console.WriteLine($"Dictionary lookup: Grace is {graceAge}");
                    }

                    var uniqueLanguages = new HashSet<string> { "C#", "F#", "C#", "Python" };
                    Console.WriteLine($"HashSet (unique only): {string.Join(", ", uniqueLanguages)}");

                    var printQueue = new Queue<string>();
                    printQueue.Enqueue("job-1");
                    printQueue.Enqueue("job-2");
                    Console.WriteLine($"Queue (FIFO): next to print is {printQueue.Dequeue()}");

                    var undoStack = new Stack<string>();
                    undoStack.Push("type 'hello'");
                    undoStack.Push("delete 'hello'");
                    Console.WriteLine($"Stack (LIFO): undo last action -> {undoStack.Pop()}");
                    """
                },
                new()
                {
                    Id = "snip_learn_collections_generic_constraint",
                    Title = "A Generic Method with an IComparable<T> Constraint",
                    Description = "One FindMax<T> method works for ints, strings, or any custom type that implements IComparable<T>.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;

                    var numbers = new List<int> { 4, 19, 7, 2, 11 };
                    Console.WriteLine($"Max number: {FindMax(numbers)}");

                    var words = new List<string> { "banana", "kiwi", "apple" };
                    Console.WriteLine($"Max word: {FindMax(words)}");

                    var scores = new List<PlayerScore>
                    {
                        new("Ada", 42),
                        new("Grace", 99),
                        new("Alan", 77)
                    };
                    Console.WriteLine($"Top score: {FindMax(scores)}");

                    static T FindMax<T>(List<T> items) where T : IComparable<T>
                    {
                        if (items.Count == 0)
                        {
                            throw new InvalidOperationException("Cannot find max of an empty list.");
                        }

                        T max = items[0];
                        foreach (T item in items)
                        {
                            if (item.CompareTo(max) > 0)
                            {
                                max = item;
                            }
                        }

                        return max;
                    }

                    record PlayerScore(string Player, int Points) : IComparable<PlayerScore>
                    {
                        public int CompareTo(PlayerScore? other) => Points.CompareTo(other?.Points ?? 0);
                        public override string ToString() => $"{Player} ({Points} pts)";
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_collections_custom",
                    Title = "A Minimal Custom Collection with IEnumerable<T>",
                    Description = "Implement IEnumerable<T> with a yield-return iterator so a custom type works in foreach and with LINQ.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections;
                    using System.Collections.Generic;
                    using System.Linq;

                    var buffer = new CircularBuffer<int>(capacity: 3);
                    buffer.Add(1);
                    buffer.Add(2);
                    buffer.Add(3);
                    buffer.Add(4); // overwrites the oldest item (1)

                    foreach (int value in buffer)
                    {
                        Console.WriteLine($"Buffer item: {value}");
                    }

                    Console.WriteLine($"Sum via LINQ: {buffer.Sum()}");

                    class CircularBuffer<T> : IEnumerable<T>
                    {
                        private readonly T[] _items;
                        private int _count;
                        private int _start;

                        public CircularBuffer(int capacity) => _items = new T[capacity];

                        public void Add(T item)
                        {
                            int writeIndex = (_start + _count) % _items.Length;
                            _items[writeIndex] = item;

                            if (_count < _items.Length)
                            {
                                _count++;
                            }
                            else
                            {
                                _start = (_start + 1) % _items.Length;
                            }
                        }

                        public IEnumerator<T> GetEnumerator()
                        {
                            for (int i = 0; i < _count; i++)
                            {
                                yield return _items[(_start + i) % _items.Length];
                            }
                        }

                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                    """
                }
            }
        };
    }
}
