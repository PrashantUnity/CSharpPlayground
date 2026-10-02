using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildExceptionLoggingCategory()
    {
        return new DocCategory
        {
            Id = "exception_logging",
            Title = "Exception Handling & Logging",
            IconKind = MaterialIconKind.AlertCircleOutline,
            AccentColor = "#F87171",
            Badge = "Reliability",
            Description = "Custom exceptions, global exception handling, structured logging, and retry patterns.",
            Articles = new List<DocArticle>
            {
                CreateExceptionLoggingArticle()
            }
        };
    }

    private DocArticle CreateExceptionLoggingArticle()
    {
        return new DocArticle
        {
            Id = "learn_exception_logging",
            Title = "Exceptions, Logging & Making Code Fail Gracefully",
            Subtitle = "Design meaningful custom exceptions, catch what you can handle, log what you can't, and retry the failures that are worth retrying.",
            ReadingTime = "7 min read",
            Summary = "Reliable software doesn't avoid failure — it anticipates it. This chapter covers writing your own exception types, catching unhandled failures at the edges of a process, the standard logging abstraction the .NET ecosystem is built around, and simple patterns for retrying transient faults.",
            Keywords = new List<string> { "exception", "custom exception", "inner exception", "appdomain.unhandledexception", "taskscheduler.unobservedtaskexception", "ilogger", "serilog", "nlog", "structured logging", "retry", "exponential backoff", "fault tolerance" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Custom Exceptions",
                    Content = "The built-in exception types (InvalidOperationException, ArgumentException, and friends) cover generic problems. When your own code has a specific failure mode that callers might want to catch and handle differently from everything else, define a dedicated exception type by deriving from Exception. Give it the three standard constructors so it behaves like every other .NET exception, and add any extra properties that help a caller understand what went wrong.",
                    BulletPoints = new List<string>
                    {
                        "Derive from Exception (or a more specific existing type) — never from a non-exception base.",
                        "Always provide the parameterless, message, and message+innerException constructors.",
                        "Add extra properties (an error code, an entity id) rather than parsing information back out of the Message string."
                    }
                },
                new()
                {
                    Heading = "Preserving the Inner Exception",
                    Content = "When you catch a low-level exception and want to raise a more meaningful one in its place, always pass the original as innerException rather than discarding it. Losing it destroys the original stack trace and root cause, turning an easy diagnosis into a guessing game.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "throw; (bare, with no exception object) rethrows the current exception while preserving its original stack trace. throw ex; resets the stack trace to the current line, hiding where it actually happened — avoid it unless you deliberately mean to reset the trace."
                },
                new()
                {
                    Heading = "Global Exception Handling",
                    Content = "However carefully you write try/catch blocks, some exceptions will still escape every one of them — a bug, a scenario nobody anticipated. Two process-wide events exist specifically to observe those: AppDomain.CurrentDomain.UnhandledException fires for any exception that unwinds out of every catch block on any thread (by the time it fires, the process is already terminating), and TaskScheduler.UnobservedTaskException fires when a faulted Task is garbage-collected without anyone ever having observed its exception (via await, .Result, or .Exception).",
                    BulletPoints = new List<string>
                    {
                        "AppDomain.UnhandledException — a last-chance hook to log a crash before the process exits; it cannot stop the crash.",
                        "TaskScheduler.UnobservedTaskException — catches \"fire-and-forget\" tasks whose failures were silently dropped; call SetObserved() on the event args once logged to prevent it escalating further.",
                        "Neither event is a substitute for handling exceptions close to where they occur — they're a safety net for what slips through."
                    }
                },
                new()
                {
                    Heading = "Logging: ILogger, Serilog, and NLog",
                    Content = "Microsoft.Extensions.Logging defines ILogger as the standard abstraction almost every modern .NET library and app codes against, regardless of which concrete logging backend is used underneath. Serilog and NLog are two of the most popular backends: they plug into that same ILogger abstraction (via provider packages) and add features like structured, queryable log properties and \"sinks\" that write to files, the console, or external log-aggregation services. Because this project is a self-contained script host rather than an ASP.NET application with a dependency-injection container already wired up, the snippets below use a tiny hand-written logger that mirrors ILogger's shape (LogInformation/LogWarning/LogError with structured placeholders) without requiring any package reference to run.",
                    BulletPoints = new List<string>
                    {
                        "ILogger.LogInformation(\"User {UserId} signed in\", userId) — the {UserId} placeholder becomes a structured, queryable field in the log output, not just text baked into a string.",
                        "Prefer structured placeholders over string interpolation in log messages — it lets log viewers filter and aggregate by field instead of grepping text.",
                        "Log levels (Trace, Debug, Information, Warning, Error, Critical) let you dial verbosity up or down per environment without changing code."
                    }
                },
                new()
                {
                    Heading = "Retry & Fault Tolerance",
                    Content = "Many failures are transient — a network blip, a momentarily unavailable service — and simply succeed if you try again a moment later. A retry loop with exponential backoff (waiting longer between each attempt) handles these gracefully without hammering a struggling dependency. Reserve retries for operations that are safe to repeat (idempotent), and always give up after a bounded number of attempts rather than retrying forever.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Only retry exceptions you know are transient (timeouts, connection failures) — blindly retrying every exception can turn a fast, clear failure (like an invalid argument) into a slow, confusing one."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "YourException(string message, Exception innerException)", ReturnType = "constructor", Parameters = "string message, Exception innerException", Description = "The standard constructor pattern every custom exception should expose, alongside a parameterless and a message-only overload." },
                new() { MethodName = "AppDomain.UnhandledException", ReturnType = "event UnhandledExceptionEventHandler", Parameters = "object sender, UnhandledExceptionEventArgs e", Description = "Raised when an exception escapes every catch block on any thread in the process; by this point the process is unwinding and cannot be saved, only logged." },
                new() { MethodName = "TaskScheduler.UnobservedTaskException", ReturnType = "event EventHandler<UnobservedTaskExceptionEventArgs>", Parameters = "object sender, UnobservedTaskExceptionEventArgs e", Description = "Raised when a faulted Task's exception was never observed before the Task was garbage-collected — the classic \"fire-and-forget\" leak." },
                new() { MethodName = "ILogger.LogInformation / LogError", ReturnType = "void", Parameters = "string message, params object[] args", Description = "Microsoft.Extensions.Logging's standard structured-logging calls — message is a template with {PlaceholderName} tokens filled from args." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_exception_logging_custom_exception",
                    Title = "A Custom Exception with an Inner Exception",
                    Description = "Define a domain-specific exception, wrap a lower-level failure inside it, and inspect both.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    public class InvoiceProcessingException : Exception
                    {
                        public string InvoiceId { get; }

                        public InvoiceProcessingException(string invoiceId, string message)
                            : base(message)
                        {
                            InvoiceId = invoiceId;
                        }

                        public InvoiceProcessingException(string invoiceId, string message, Exception innerException)
                            : base(message, innerException)
                        {
                            InvoiceId = invoiceId;
                        }
                    }

                    void ParseInvoiceTotal(string invoiceId, string rawTotal)
                    {
                        try
                        {
                            decimal.Parse(rawTotal);
                        }
                        catch (FormatException ex)
                        {
                            // Wrap the low-level parse failure in a domain-specific exception,
                            // preserving the original as InnerException instead of discarding it.
                            throw new InvoiceProcessingException(invoiceId, $"Invoice {invoiceId} has an unparsable total: '{rawTotal}'.", ex);
                        }
                    }

                    try
                    {
                        ParseInvoiceTotal("INV-1042", "not-a-number");
                    }
                    catch (InvoiceProcessingException ex)
                    {
                        Console.WriteLine($"Failed: {ex.Message} (InvoiceId={ex.InvoiceId})");
                        Console.WriteLine($"Root cause: {ex.InnerException?.GetType().Name} - {ex.InnerException?.Message}");
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_exception_logging_retry_backoff",
                    Title = "Retry with Exponential Backoff",
                    Description = "A reusable async retry helper that backs off between attempts and gives up after a fixed number of tries.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading.Tasks;

                    async Task<T> RetryWithBackoffAsync<T>(Func<Task<T>> operation, int maxAttempts = 4)
                    {
                        for (int attempt = 1; attempt <= maxAttempts; attempt++)
                        {
                            try
                            {
                                return await operation();
                            }
                            catch (Exception ex) when (attempt < maxAttempts)
                            {
                                // Only transient-looking failures should reach here in real code —
                                // this demo retries every exception to keep the sample self-contained.
                                var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)); // 100ms, 200ms, 400ms, ...
                                Console.WriteLine($"Attempt {attempt} failed ({ex.Message}); retrying in {delay.TotalMilliseconds:N0} ms.");
                                await Task.Delay(delay);
                            }
                        }

                        // Final attempt: let any exception propagate to the caller.
                        return await operation();
                    }

                    int callCount = 0;

                    Task<string> FlakyOperationAsync()
                    {
                        callCount++;
                        if (callCount < 3)
                        {
                            throw new InvalidOperationException($"Simulated transient failure #{callCount}.");
                        }
                        return Task.FromResult("Success!");
                    }

                    string result = await RetryWithBackoffAsync(FlakyOperationAsync);
                    Console.WriteLine($"Result: {result} (after {callCount} attempts).");
                    """
                },
                new()
                {
                    Id = "snip_learn_exception_logging_structured_logging",
                    Title = "A Minimal Structured Logger (Stand-In for ILogger)",
                    Description = "A dependency-free logger shaped like ILogger, showing how structured placeholders map to fields instead of plain text.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Text.RegularExpressions;

                    // A tiny stand-in for Microsoft.Extensions.Logging.ILogger: in a real app this would be
                    // injected and backed by Serilog, NLog, or another provider, but the calling code below
                    // would look identical either way.
                    static class SimpleLog
                    {
                        public static void Information(string template, params object[] args) => Write("INFO", template, args);
                        public static void Error(string template, params object[] args) => Write("ERROR", template, args);

                        private static void Write(string level, string template, object[] args)
                        {
                            int i = 0;
                            string rendered = Regex.Replace(template, "\\{[^}]+\\}", _ => Convert.ToString(args[i++]) ?? "null");
                            Console.WriteLine($"[{level}] {rendered}   (template: \"{template}\")");
                        }
                    }

                    string userId = "u-4471";
                    int itemCount = 3;

                    // {UserId} and {ItemCount} are structured placeholders, not string interpolation —
                    // a real logging backend keeps them as separate, queryable fields alongside the message.
                    SimpleLog.Information("User {UserId} checked out {ItemCount} items.", userId, itemCount);
                    SimpleLog.Error("Checkout failed for user {UserId}: {Reason}", userId, "payment declined");
                    """
                }
            }
        };
    }
}
