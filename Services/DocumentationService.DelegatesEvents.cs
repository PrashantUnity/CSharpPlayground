using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildDelegatesEventsCategory()
    {
        return new DocCategory
        {
            Id = "delegates_events",
            Title = "Delegates, Events & Functional Programming",
            IconKind = MaterialIconKind.FunctionVariant,
            AccentColor = "#FB923C",
            Badge = "Functional",
            Description = "Delegates, multicast delegates, events, and functional-style C# with lambdas.",
            Articles = new List<DocArticle>
            {
                CreateDelegatesEventsArticle()
            }
        };
    }

    private DocArticle CreateDelegatesEventsArticle()
    {
        return new DocArticle
        {
            Id = "learn_delegates_events",
            Title = "Delegates, Events & Functional Programming",
            Subtitle = "Treat methods as values you can store, pass around, combine, and invoke later.",
            ReadingTime = "7 min read",
            Summary = "A delegate is a type-safe reference to a method — the foundation events are built on, and the mechanism that lets lambdas and method groups flow through your program as ordinary values.",
            Keywords = new List<string> { "delegate", "multicast delegate", "event", "eventhandler", "func", "action", "predicate", "lambda", "anonymous method", "callback", "publisher subscriber" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Delegates & Multicast Delegates",
                    Content = "A delegate type declares a method signature — return type and parameters — that any matching method can be assigned to. Once you have a delegate instance, calling it invokes whichever method it points to, without the caller needing to know which one that is. Delegates are multicast: the += operator chains another method onto the same delegate instance, and invoking it calls every chained method in the order they were added.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "If a multicast delegate has a non-void return type, invoking it still calls every subscriber, but only the return value of the LAST one in the chain is returned to the caller."
                },
                new()
                {
                    Heading = "Events",
                    Content = "The `event` keyword wraps a delegate field so that code outside the declaring class can only += or -= a handler to it, not invoke it directly or overwrite the whole chain with =. That protects the publisher/subscriber pattern: the class that owns the event decides when to raise it, and any number of subscribers can react without knowing about each other.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Always check an event for null before raising it (e.g. `SomethingHappened?.Invoke(this, args)`) — an event with no subscribers is null, and invoking a null delegate throws a NullReferenceException."
                },
                new()
                {
                    Heading = "Func<>, Action<> & Predicate<>",
                    Content = "Rather than declaring a new delegate type for every signature, .NET provides generic ones ready to use. Func<T1, ..., TResult> points to a method that returns a value (the last type parameter is always the return type). Action<T1, ...> points to a method that returns void. Predicate<T> points to a method taking one T and returning bool — used throughout the collection APIs, like List<T>.Find and List<T>.RemoveAll."
                },
                new()
                {
                    Heading = "Lambda Expressions",
                    Content = "A lambda expression, `(parameters) => expression-or-block`, is a compact, inline way to write the method body a delegate points to, without declaring a separate named method. Lambdas can capture variables from the surrounding scope (a \"closure\"), which is what lets a short lambda passed to Where or Select still reference a local variable defined outside it."
                },
                new()
                {
                    Heading = "Anonymous Methods",
                    Content = "Before lambdas existed (C# 2.0), the `delegate { ... }` syntax let you write an inline method body without naming it — an anonymous method. Lambdas have almost entirely replaced this syntax because they're shorter and support expression bodies, but you'll still see `delegate` blocks in older codebases, and both compile down to the same kind of delegate instance."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Func<T, TResult>.Invoke", ReturnType = "TResult", Parameters = "T arg", Description = "Calls the wrapped method with the given argument and returns its result; Func<T,TResult> itself is invoked implicitly whenever you call the delegate variable like a method." },
                new() { MethodName = "Action<T>.Invoke", ReturnType = "void", Parameters = "T arg", Description = "Calls the wrapped void-returning method with the given argument." },
                new() { MethodName = "EventHandler", ReturnType = "void", Parameters = "object? sender, EventArgs e", Description = "The standard non-generic delegate shape for a parameterless event, carrying the sender and an EventArgs (or EventArgs.Empty)." },
                new() { MethodName = "EventHandler<TEventArgs>", ReturnType = "void", Parameters = "object? sender, TEventArgs e", Description = "The generic form of EventHandler, used when an event needs to carry custom data via a TEventArgs subclass of EventArgs." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_delegates_multicast",
                    Title = "Custom Delegate & Multicast Delegate",
                    Description = "Declare a delegate type, then chain several methods onto one instance with +=.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    delegate void NotifyDelegate(string message);

                    NotifyDelegate notify = LogToConsole;
                    notify += LogWithTimestamp;
                    notify += Beep;

                    // Invoking once calls all three subscribers, in order.
                    notify("Build completed successfully.");

                    notify -= Beep;
                    Console.WriteLine("--- After removing Beep ---");
                    notify("Build completed successfully.");

                    static void LogToConsole(string message) => Console.WriteLine($"[LOG] {message}");
                    static void LogWithTimestamp(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                    static void Beep(string message) => Console.WriteLine("[BEEP] (imagine a sound here)");
                    """
                },
                new()
                {
                    Id = "snip_learn_delegates_events",
                    Title = "Publisher/Subscriber with a C# Event",
                    Description = "A ProgressReporter raises an event that any number of subscribers can listen to.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    var reporter = new ProgressReporter();

                    reporter.ProgressChanged += (sender, e) => Console.WriteLine($"Console subscriber: {e.PercentComplete}% done");
                    reporter.ProgressChanged += LogProgressToHistory;

                    reporter.Run();

                    static void LogProgressToHistory(object? sender, ProgressEventArgs e)
                    {
                        Console.WriteLine($"History subscriber: recorded {e.PercentComplete}%");
                    }

                    class ProgressEventArgs : EventArgs
                    {
                        public int PercentComplete { get; init; }
                    }

                    class ProgressReporter
                    {
                        public event EventHandler<ProgressEventArgs>? ProgressChanged;

                        public void Run()
                        {
                            for (int percent = 0; percent <= 100; percent += 50)
                            {
                                OnProgressChanged(percent);
                            }
                        }

                        private void OnProgressChanged(int percent)
                        {
                            ProgressChanged?.Invoke(this, new ProgressEventArgs { PercentComplete = percent });
                        }
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_delegates_func_action_predicate",
                    Title = "Func, Action, Predicate & Lambdas",
                    Description = "Pass lambdas as Func, Action, and Predicate arguments, including to List<T>.Find and Where.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;
                    using System.Linq;

                    var numbers = new List<int> { 4, 15, 8, 23, 42, 7 };

                    // Func<T, TResult>: takes an int, returns a bool.
                    Func<int, bool> isEven = n => n % 2 == 0;
                    Console.WriteLine($"Is 42 even? {isEven(42)}");

                    // Action<T>: takes a value, returns nothing.
                    Action<int> printSquare = n => Console.WriteLine($"{n} squared is {n * n}");
                    printSquare(6);

                    // Predicate<T>: same shape as Func<T, bool>, used by List<T>.Find/RemoveAll.
                    Predicate<int> isOverTwenty = n => n > 20;
                    int? firstOverTwenty = numbers.Find(isOverTwenty);
                    Console.WriteLine($"First number over 20: {firstOverTwenty}");

                    List<int> evens = numbers.Where(isEven).ToList();
                    Console.WriteLine($"Even numbers: {string.Join(", ", evens)}");
                    """
                }
            }
        };
    }
}
