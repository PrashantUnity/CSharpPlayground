using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildDesignPatternsCategory()
    {
        return new DocCategory
        {
            Id = "design_patterns",
            Title = "Design Patterns",
            IconKind = MaterialIconKind.VectorSquare,
            AccentColor = "#FACC15",
            Badge = "Patterns",
            Description = "Classic design patterns and architectural patterns commonly used in C# applications.",
            Articles = new List<DocArticle>
            {
                CreateDesignPatternsArticle()
            }
        };
    }

    private DocArticle CreateDesignPatternsArticle()
    {
        return new DocArticle
        {
            Id = "learn_design_patterns",
            Title = "Design Patterns in C#",
            Subtitle = "Named, reusable solutions to recurring design problems — Singleton, Factory, Repository, Strategy, Observer, Unit of Work, and MVC/MVVM.",
            ReadingTime = "10 min read",
            Summary = "A design pattern isn't a library you install — it's a shared vocabulary for a shape of solution that keeps coming up. Recognizing that you're looking at a Factory or a Strategy problem lets you reach for a well-understood solution instead of inventing something bespoke, and lets other developers recognize your intent instantly.",
            Keywords = new List<string> { "design pattern", "singleton", "factory", "repository", "strategy", "observer", "unit of work", "mvc", "mvvm", "lazy<t>", "iobserver", "iobservable", "creational pattern", "behavioral pattern", "structural pattern" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Singleton",
                    Content = "The Singleton pattern ensures a class has exactly one instance for the lifetime of the application, and provides a single, well-known way to access it. It's useful for shared resources like a configuration object or a cache where having two independent instances would cause inconsistent state. This DocumentationService class itself is registered and consumed as a singleton in this application — one instance built once, then handed out everywhere the documentation is needed.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Singleton is one of the most overused patterns. It introduces global, shared mutable state, which makes unit testing harder (tests can leak state into each other) and hides a class's dependencies. Prefer registering a class as a singleton in a DI container over hand-rolling the pattern — you get the 'one instance' behavior without the global static access point."
                },
                new()
                {
                    Heading = "Factory",
                    Content = "The Factory pattern moves the logic for deciding which concrete class to instantiate out of client code and into a dedicated method or class. Instead of a caller writing `new ConcreteThing()` and being coupled to that specific type, it asks a factory for 'a Thing' and receives whichever implementation is appropriate — chosen by some input, configuration, or runtime condition.",
                    BulletPoints = new List<string>
                    {
                        "A simple factory is often just a static method with a switch expression that returns different implementations of a shared interface.",
                        "The Factory Method pattern goes further and lets subclasses override which concrete type gets created.",
                        "Factories pair naturally with Dependency Injection: a container is, in effect, a very general-purpose factory for your registered services."
                    }
                },
                new()
                {
                    Heading = "Repository",
                    Content = "The Repository pattern puts a collection-like interface (Add, GetById, Find, Remove) in front of however data is actually stored, whether that's a SQL database, a document store, or an in-memory list during tests. Business logic asks the repository for entities and never sees SQL, an ORM's change tracker, or connection strings — which lets you swap the storage technology, or substitute a fake repository in tests, without touching the code that consumes it.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Modern ORMs like Entity Framework Core's DbSet<T> already provide a repository-shaped API. Wrapping EF Core in another hand-written repository layer is sometimes unnecessary — evaluate whether it's adding real abstraction or just extra indirection for your project."
                },
                new()
                {
                    Heading = "Strategy",
                    Content = "The Strategy pattern extracts an algorithm or behavior into its own interface, so it can be swapped out at runtime without changing the code that uses it. Instead of a method full of if/else or switch branches choosing between behaviors, the caller is handed (or selects) a strategy object, and always calls the same method on it — the strategy decides what actually happens.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Strategy is what the Open/Closed Principle looks like in practice: adding a new algorithm means adding a new class that implements the strategy interface, not editing an existing one."
                },
                new()
                {
                    Heading = "Observer",
                    Content = "The Observer pattern lets one object (the subject) notify a list of interested objects (observers) whenever something happens, without the subject needing to know any concrete detail about who's listening or what they'll do. C#'s built-in event keyword is a language-level implementation of this pattern; System.IObservable<T> and System.IObserver<T> formalize the same idea as a pair of interfaces, with IObservable<T>.Subscribe registering an IObserver<T> to receive OnNext/OnError/OnCompleted calls.",
                    BulletPoints = new List<string>
                    {
                        "C# events (using event and EventHandler/EventHandler<T>) are the idiomatic choice for most UI and application code.",
                        "IObservable<T>/IObserver<T> are the basis of Reactive Extensions (Rx.NET) and are useful when you need to compose, filter, or combine streams of notifications.",
                        "Either way, the subject is decoupled from its observers — it can gain or lose subscribers without any code change to itself."
                    }
                },
                new()
                {
                    Heading = "Unit of Work",
                    Content = "The Unit of Work pattern groups a set of changes across one or more repositories into a single transaction: either everything commits together, or nothing does. It tracks what changed during a business operation and coordinates saving all of it in one call, which avoids the problem of a partial update leaving data inconsistent when one repository's save succeeds and another's fails. Entity Framework Core's DbContext already implements this pattern — SaveChanges() commits every tracked change (across every DbSet) as one unit.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "If you're already using EF Core's DbContext, you likely already have a Unit of Work — introducing a second, hand-rolled one on top is usually redundant."
                },
                new()
                {
                    Heading = "MVC / MVVM",
                    Content = "Model-View-Controller and Model-View-ViewModel both split an application into three cooperating parts to keep UI code separate from business logic and data. In MVC, the Controller receives input, updates the Model, and selects a View to render — common in web frameworks like ASP.NET Core MVC. In MVVM, common in Avalonia, WPF, and other XAML-based UI frameworks, the View binds declaratively to a ViewModel's properties and commands; the ViewModel exposes state and behavior without holding any reference to the View itself, which is what makes the ViewModel unit-testable without a UI to render.",
                    BulletPoints = new List<string>
                    {
                        "Model: the data and business rules, independent of any UI concern.",
                        "View: the visual layer — a Razor page in MVC, or an .axaml/.xaml file in MVVM — responsible only for presentation.",
                        "Controller (MVC): receives requests, coordinates the Model, and chooses a View.",
                        "ViewModel (MVVM): exposes bindable properties and commands the View data-binds to, with no direct reference back to the View."
                    }
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "IObservable<T>.Subscribe", ReturnType = "IDisposable", Parameters = "IObserver<T> observer", Description = "Registers an observer to receive notifications from this observable sequence; disposing the returned handle unsubscribes it." },
                new() { MethodName = "IObserver<T>.OnNext", ReturnType = "void", Parameters = "T value", Description = "Called by the observable source to push the next value to a subscribed observer." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_patterns_singleton",
                    Title = "Thread-Safe Singleton with Lazy<T>",
                    Description = "Lazy<T> gives you a thread-safe, lazily-created single instance without writing any locking code yourself - the same shape this app's own DocumentationService uses.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    public sealed class AppSettings
                    {
                        // The Lazy<T> ensures the instance is created at most once, even if
                        // multiple threads access Instance at the same time.
                        private static readonly Lazy<AppSettings> _instance =
                            new(() => new AppSettings());

                        public static AppSettings Instance => _instance.Value;

                        public string Theme { get; set; } = "Dark";
                        public int MaxRecentFiles { get; set; } = 10;

                        // A private constructor prevents anyone outside this class from
                        // creating additional instances with `new AppSettings()`.
                        private AppSettings()
                        {
                            Console.WriteLine("AppSettings constructed exactly once.");
                        }
                    }

                    Console.WriteLine(AppSettings.Instance.Theme);

                    AppSettings.Instance.Theme = "Light";

                    // Every reference to Instance points at the same object.
                    Console.WriteLine($"Same instance? {ReferenceEquals(AppSettings.Instance, AppSettings.Instance)}");
                    Console.WriteLine($"Updated theme seen everywhere: {AppSettings.Instance.Theme}");
                    """
                },
                new()
                {
                    Id = "snip_learn_patterns_factory_strategy",
                    Title = "Factory + Strategy Combo",
                    Description = "A factory selects which pricing strategy to use, and each strategy implements the same interface differently.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    // Strategy: a common interface for interchangeable algorithms.
                    public interface IDiscountStrategy
                    {
                        decimal Apply(decimal subtotal);
                    }

                    public class NoDiscount : IDiscountStrategy
                    {
                        public decimal Apply(decimal subtotal) => subtotal;
                    }

                    public class PercentageDiscount : IDiscountStrategy
                    {
                        private readonly decimal _percent;
                        public PercentageDiscount(decimal percent) => _percent = percent;
                        public decimal Apply(decimal subtotal) => subtotal - (subtotal * _percent / 100m);
                    }

                    public class FlatAmountDiscount : IDiscountStrategy
                    {
                        private readonly decimal _amount;
                        public FlatAmountDiscount(decimal amount) => _amount = amount;
                        public decimal Apply(decimal subtotal) => Math.Max(0, subtotal - _amount);
                    }

                    // Factory: chooses which strategy to hand back based on the customer's tier.
                    public static class DiscountStrategyFactory
                    {
                        public static IDiscountStrategy Create(string customerTier) => customerTier switch
                        {
                            "Gold" => new PercentageDiscount(15m),
                            "Silver" => new PercentageDiscount(5m),
                            "Coupon10" => new FlatAmountDiscount(10m),
                            _ => new NoDiscount()
                        };
                    }

                    decimal subtotal = 200m;
                    foreach (string tier in new[] { "Gold", "Silver", "Coupon10", "Standard" })
                    {
                        IDiscountStrategy strategy = DiscountStrategyFactory.Create(tier);
                        Console.WriteLine($"{tier,-10} -> {strategy.Apply(subtotal):C}");
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_patterns_observer",
                    Title = "Observer Pattern with C# Events",
                    Description = "A StockTicker (the subject) raises an event whenever its price changes, and any number of observers can subscribe without the ticker knowing anything about them.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    public class PriceChangedEventArgs : EventArgs
                    {
                        public string Symbol { get; }
                        public decimal NewPrice { get; }
                        public PriceChangedEventArgs(string symbol, decimal newPrice)
                        {
                            Symbol = symbol;
                            NewPrice = newPrice;
                        }
                    }

                    // The subject: exposes an event, has no idea who (if anyone) is listening.
                    public class StockTicker
                    {
                        public string Symbol { get; }
                        private decimal _price;

                        public event EventHandler<PriceChangedEventArgs>? PriceChanged;

                        public StockTicker(string symbol, decimal startingPrice)
                        {
                            Symbol = symbol;
                            _price = startingPrice;
                        }

                        public void UpdatePrice(decimal newPrice)
                        {
                            _price = newPrice;
                            PriceChanged?.Invoke(this, new PriceChangedEventArgs(Symbol, newPrice));
                        }
                    }

                    var ticker = new StockTicker("MSFT", 415.00m);

                    // Observer #1: logs every change.
                    ticker.PriceChanged += (sender, e) =>
                        Console.WriteLine($"[Log] {e.Symbol} changed to {e.NewPrice:C}");

                    // Observer #2: raises an alert only above a threshold.
                    ticker.PriceChanged += (sender, e) =>
                    {
                        if (e.NewPrice > 420m)
                        {
                            Console.WriteLine($"[Alert] {e.Symbol} crossed 420: {e.NewPrice:C}");
                        }
                    };

                    ticker.UpdatePrice(417.50m);
                    ticker.UpdatePrice(422.10m);
                    """
                }
            }
        };
    }
}
