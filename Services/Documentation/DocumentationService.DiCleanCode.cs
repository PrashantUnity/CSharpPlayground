using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildDiCleanCodeCategory()
    {
        return new DocCategory
        {
            Id = "di_clean_code",
            Title = "Dependency Injection & Clean Code",
            IconKind = MaterialIconKind.Puzzle,
            AccentColor = "#C084FC",
            Badge = "Architecture",
            Description = "Dependency injection, inversion of control, SOLID principles, and clean architecture.",
            Articles = new List<DocArticle>
            {
                CreateDiCleanCodeArticle()
            }
        };
    }

    private DocArticle CreateDiCleanCodeArticle()
    {
        return new DocArticle
        {
            Id = "learn_di_clean_code",
            Title = "Dependency Injection & Clean Code",
            Subtitle = "Write classes that depend on abstractions instead of concrete implementations, and structure a codebase so change in one place doesn't ripple everywhere.",
            ReadingTime = "9 min read",
            Summary = "Dependency Injection and the SOLID principles aren't academic exercises — they're the difference between a codebase you can change confidently and one where every fix risks breaking something unrelated. This article builds the ideas up from plain constructor injection, with no framework required, before showing where a DI container fits in.",
            Keywords = new List<string> { "dependency injection", "di", "inversion of control", "ioc", "solid", "single responsibility", "open closed", "liskov substitution", "interface segregation", "dependency inversion", "clean architecture", "separation of concerns", "iservicecollection", "addscoped", "addsingleton", "addtransient" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Dependency Injection (DI)",
                    Content = "Dependency Injection is a technique where a class receives the objects it depends on from the outside — typically through its constructor — instead of creating them itself with `new`. A class that constructs its own SmtpClient or DbContext is welded to that specific implementation; a class that accepts an IEmailSender or IOrderRepository through its constructor can be handed a different implementation (a real one, a test double, a logging wrapper) without changing a single line of the class itself.",
                    BulletPoints = new List<string>
                    {
                        "Constructor injection is the most common form: dependencies arrive as constructor parameters and are stored in readonly fields.",
                        "It makes unit testing dramatically simpler — pass a fake or mock implementation of the dependency instead of hitting a real database or network service.",
                        "No framework is required to practice DI; 'dependency injection' describes the pattern of passing dependencies in, not any particular library."
                    },
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "A quick smell test: if a class's constructor takes interfaces (IEmailSender, ILogger, IOrderRepository) rather than concrete classes, it's practicing DI. If it says `new SmtpClient()` inside a method, it isn't."
                },
                new()
                {
                    Heading = "Inversion of Control (IoC)",
                    Content = "Inversion of Control is the broader principle that DI is one specific technique for: instead of your code controlling when and how its dependencies are created, that control is handed to something else — a caller, a framework, or a DI container. Traditionally, a class controls its own flow and reaches out to create whatever it needs. With IoC, that's inverted: the class declares what it needs (via its constructor), and an external piece of code decides what concrete instance to provide.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "DI is IoC applied specifically to object construction. Other forms of IoC include the template method pattern and event-driven callbacks, where a framework calls back into your code rather than your code driving the framework."
                },
                new()
                {
                    Heading = "SOLID Principles",
                    Content = "SOLID is an acronym for five design principles that keep object-oriented code adaptable to change. They're guidelines, not laws — the goal is to recognize when a class is taking on too much, or when a change in one place is forcing changes somewhere unrelated, and knowing which principle names that problem.",
                    BulletPoints = new List<string>
                    {
                        "S — Single Responsibility Principle: a class should have one reason to change. A ReportGenerator that also handles emailing and file compression has three reasons to change, and a bug fix to one risks breaking the others.",
                        "O — Open/Closed Principle: classes should be open for extension but closed for modification. Add new behavior by adding new code (a new implementation of an interface) rather than editing an existing, already-tested class.",
                        "L — Liskov Substitution Principle: a subtype must be usable anywhere its base type is expected without breaking correctness. If swapping in a derived class changes the caller's expected behavior in a surprising way, the inheritance is wrong.",
                        "I — Interface Segregation Principle: prefer several small, focused interfaces over one large interface that forces implementers to support methods they don't need.",
                        "D — Dependency Inversion Principle: high-level modules shouldn't depend on low-level modules directly — both should depend on abstractions. This is the principle that DI puts into practice."
                    },
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "SOLID principles can be over-applied. Splitting a three-line class into five interfaces 'for flexibility' you'll never use is its own kind of complexity — apply each principle when it solves a real problem you have, not preemptively."
                },
                new()
                {
                    Heading = "Clean Architecture",
                    Content = "Clean Architecture (and similar layered approaches like Hexagonal or Onion architecture) organizes a codebase into concentric layers, with a single rule: dependencies only point inward. Your core domain logic and business rules sit at the center and know nothing about the outside world — not the database, not the UI framework, not the web API. Outer layers (data access, UI, external services) depend on the core through interfaces the core defines, never the other way around.",
                    BulletPoints = new List<string>
                    {
                        "The domain/core layer contains entities and business rules, with zero references to Entity Framework, ASP.NET, or any specific database or UI technology.",
                        "The application layer orchestrates use cases (e.g. 'place an order') by depending on interfaces like IOrderRepository, which the domain or application layer declares.",
                        "The infrastructure layer implements those interfaces against real technology — a SQL database, a REST API, the file system — and is where framework-specific code lives.",
                        "Because outer layers depend on inner ones (never the reverse), you can replace a database or UI framework without touching business logic."
                    }
                },
                new()
                {
                    Heading = "Separation of Concerns",
                    Content = "Separation of Concerns is the umbrella idea underneath SRP and Clean Architecture: each part of a system should address one concern — one problem or responsibility — and know as little as possible about the others. Validation logic, business rules, data persistence, and presentation are different concerns; mixing them into one method or class (validating input, computing a price, and formatting HTML all in one place) makes each concern harder to test, reuse, and change independently.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "A practical test for separation of concerns: could you explain what a method does in one sentence without using the word 'and'? If not, it's likely handling more than one concern."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "IServiceCollection.AddTransient", ReturnType = "IServiceCollection", Parameters = "<TService, TImplementation>()", Description = "Registers a service so the DI container creates a brand-new instance every time it's requested. Part of Microsoft.Extensions.DependencyInjection (NuGet package of the same name), the standard .NET DI container." },
                new() { MethodName = "IServiceCollection.AddScoped", ReturnType = "IServiceCollection", Parameters = "<TService, TImplementation>()", Description = "Registers a service so one instance is shared within a single scope (e.g. one web request), and a new instance is created per scope. Also part of Microsoft.Extensions.DependencyInjection." },
                new() { MethodName = "IServiceCollection.AddSingleton", ReturnType = "IServiceCollection", Parameters = "<TService, TImplementation>()", Description = "Registers a service so exactly one instance is created and reused for the lifetime of the application. Also part of Microsoft.Extensions.DependencyInjection." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_di_constructor_injection",
                    Title = "Manual Constructor Injection (No Framework Needed)",
                    Description = "A NotificationService depends on an IEmailSender abstraction, not a concrete SmtpEmailSender — no DI container required to see the benefit.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;

                    // The abstraction NotificationService depends on.
                    public interface IEmailSender
                    {
                        void Send(string to, string subject, string body);
                    }

                    // One real implementation.
                    public class SmtpEmailSender : IEmailSender
                    {
                        public void Send(string to, string subject, string body)
                            => Console.WriteLine($"[SMTP] To: {to} | Subject: {subject} | Body: {body}");
                    }

                    // A fake used for testing or local demos, with no real network dependency.
                    public class FakeEmailSender : IEmailSender
                    {
                        public List<string> SentMessages { get; } = new();

                        public void Send(string to, string subject, string body)
                        {
                            SentMessages.Add($"{to}: {subject}");
                            Console.WriteLine($"[FAKE] Recorded message to {to} without sending anything.");
                        }
                    }

                    // NotificationService only knows about IEmailSender - it has no idea which
                    // implementation it's been given, and doesn't need to.
                    public class NotificationService
                    {
                        private readonly IEmailSender _emailSender;

                        public NotificationService(IEmailSender emailSender)
                        {
                            _emailSender = emailSender;
                        }

                        public void NotifyOrderShipped(string customerEmail, string orderId)
                        {
                            _emailSender.Send(customerEmail, "Your order has shipped!", $"Order {orderId} is on its way.");
                        }
                    }

                    // The caller decides which implementation to inject.
                    var realService = new NotificationService(new SmtpEmailSender());
                    realService.NotifyOrderShipped("ada@example.com", "A-1001");

                    var testService = new NotificationService(new FakeEmailSender());
                    testService.NotifyOrderShipped("grace@example.com", "A-1002");
                    """
                },
                new()
                {
                    Id = "snip_learn_di_solid_before_after",
                    Title = "SOLID Before & After: SRP and DIP",
                    Description = "A single class that violates SRP and DIP, refactored into two classes that each have one reason to change and depend on an abstraction.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;
                    using System.IO;

                    // BEFORE: violates SRP (formatting AND saving) and DIP (hard-coded to the file system).
                    public class InvoiceReport_Before
                    {
                        public void SaveAsText(string customer, decimal total, string path)
                        {
                            string text = $"Invoice for {customer}: {total:C}";
                            File.WriteAllText(path, text); // Can't test without touching real disk. Can't swap storage.
                        }
                    }

                    // AFTER: formatting and persistence are separate concerns, and persistence
                    // is an abstraction the report depends on rather than owns.
                    public interface IInvoiceStore
                    {
                        void Save(string fileName, string content);
                    }

                    public class FileInvoiceStore : IInvoiceStore
                    {
                        private readonly string _directory;
                        public FileInvoiceStore(string directory) => _directory = directory;

                        public void Save(string fileName, string content)
                        {
                            Directory.CreateDirectory(_directory);
                            File.WriteAllText(Path.Combine(_directory, fileName), content);
                        }
                    }

                    public class InMemoryInvoiceStore : IInvoiceStore
                    {
                        public Dictionary<string, string> Saved { get; } = new();
                        public void Save(string fileName, string content) => Saved[fileName] = content;
                    }

                    public class InvoiceReport_After
                    {
                        private readonly IInvoiceStore _store;
                        public InvoiceReport_After(IInvoiceStore store) => _store = store; // DIP: depends on an abstraction.

                        // SRP: this class's only job is formatting the invoice text.
                        public string Format(string customer, decimal total) => $"Invoice for {customer}: {total:C}";

                        public void SaveAsText(string customer, decimal total, string fileName)
                            => _store.Save(fileName, Format(customer, total));
                    }

                    var testStore = new InMemoryInvoiceStore();
                    var report = new InvoiceReport_After(testStore);
                    report.SaveAsText("Ada Lovelace", 249.00m, "invoice-1.txt");

                    Console.WriteLine($"Saved without touching disk: {testStore.Saved["invoice-1.txt"]}");
                    """
                },
                new()
                {
                    Id = "snip_learn_di_service_collection",
                    Title = "Registering Services with ServiceCollection",
                    Description = "Illustrative example of wiring up the same IEmailSender/NotificationService pair through the standard .NET DI container. Requires the Microsoft.Extensions.DependencyInjection NuGet package - it will not run as-is in a plain script without that reference.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using Microsoft.Extensions.DependencyInjection; // NuGet: Microsoft.Extensions.DependencyInjection

                    public interface IEmailSender
                    {
                        void Send(string to, string subject, string body);
                    }

                    public class SmtpEmailSender : IEmailSender
                    {
                        public void Send(string to, string subject, string body)
                            => Console.WriteLine($"[SMTP] To: {to} | Subject: {subject}");
                    }

                    public class NotificationService
                    {
                        private readonly IEmailSender _emailSender;
                        public NotificationService(IEmailSender emailSender) => _emailSender = emailSender;

                        public void NotifyOrderShipped(string customerEmail, string orderId)
                            => _emailSender.Send(customerEmail, "Your order has shipped!", $"Order {orderId} is on its way.");
                    }

                    var services = new ServiceCollection();
                    services.AddSingleton<IEmailSender, SmtpEmailSender>();
                    services.AddTransient<NotificationService>();

                    using ServiceProvider provider = services.BuildServiceProvider();

                    // The container resolves NotificationService's IEmailSender dependency automatically.
                    var notificationService = provider.GetRequiredService<NotificationService>();
                    notificationService.NotifyOrderShipped("ada@example.com", "A-2001");
                    """
                }
            }
        };
    }
}
