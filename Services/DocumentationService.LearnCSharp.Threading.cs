using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocArticle CreateThreadingArticle()
    {
        return new DocArticle
        {
            Id = "learn_threading",
            Title = "Threading: Thread, ThreadPool, Locks & Cancellation",
            Subtitle = "Understand when to reach for raw threads, how to protect shared state, and how to cancel work cooperatively.",
            ReadingTime = "6 min read",
            Summary = "Modern C# rarely needs a raw Thread, but understanding what Task and async/await are built on — threads, the thread pool, and synchronization primitives — makes it much easier to reason about concurrent code.",
            Keywords = new List<string> { "thread", "threadpool", "task vs thread", "queueuserworkitem", "lock", "monitor", "interlocked", "race condition", "thread safety", "cancellationtoken", "cancellationtokensource" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Thread, ThreadPool & Task — When to Use Which",
                    Content = "A raw Thread gives you a dedicated OS thread — useful for a long-running background worker, but expensive to create for short-lived work. ThreadPool.QueueUserWorkItem and Task.Run instead borrow a thread from a shared pool, which is far cheaper for brief or bursty work.",
                    BulletPoints = new List<string>
                    {
                        "Thread — a dedicated OS thread; reach for it only when you truly need a persistent, named background thread.",
                        "ThreadPool.QueueUserWorkItem — low-level access to the shared thread pool, with no built-in way to observe completion or return a result.",
                        "Task / Task.Run — the modern default: built on the thread pool, composable with await, and able to return a result or propagate an exception."
                    }
                },
                new()
                {
                    Heading = "Race Conditions, lock & Interlocked",
                    Content = "When two threads read, modify, and write the same shared variable without coordination, updates can be lost — a race condition. The lock keyword (backed by Monitor) makes a block of code run by only one thread at a time; Interlocked provides lighter-weight atomic operations for simple cases like incrementing a counter.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Never lock on this, a boxed value, or any publicly accessible object — lock on a private, dedicated readonly object field so unrelated code can never accidentally take the same lock and cause a deadlock."
                },
                new()
                {
                    Heading = "CancellationToken & Thread-Safe Collections",
                    Content = "CancellationTokenSource / CancellationToken is the standard, cooperative way to ask running work to stop — the work has to check the token (or pass it to something that does, like Task.Delay) and unwind itself; nothing forces it to stop instantly. For shared collections accessed from multiple threads, the framework also provides ready-made thread-safe types such as ConcurrentDictionary and BlockingCollection; the lock-protected Dictionary shown below is the manual equivalent when you want full control."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Interlocked.Increment", ReturnType = "int", Parameters = "ref int location", Description = "Atomically increments the given variable by one and returns the new value." },
                new() { MethodName = "Monitor.Enter / Monitor.Exit", ReturnType = "void", Parameters = "object obj", Description = "What the lock keyword compiles to — acquires and releases an exclusive lock on obj." },
                new() { MethodName = "CancellationTokenSource", ReturnType = "CancellationTokenSource", Parameters = "TimeSpan delay", Description = "Creates a token source whose token is automatically cancelled after the given delay." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_threading_thread_basics",
                    Title = "Starting a Raw Thread",
                    Description = "Create and start a dedicated Thread, then wait for it to finish.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading;

                    var worker = new Thread(() =>
                    {
                        Console.WriteLine($"Running on a dedicated thread (ManagedThreadId {Environment.CurrentManagedThreadId}).");
                        Thread.Sleep(200);
                        Console.WriteLine("Dedicated thread finished.");
                    });

                    worker.Start();
                    worker.Join(); // wait for it to finish before the script continues

                    Console.WriteLine("Back on the calling thread.");
                    """
                },
                new()
                {
                    Id = "snip_learn_threading_threadpool",
                    Title = "ThreadPool.QueueUserWorkItem",
                    Description = "Queue work onto the shared thread pool instead of creating a dedicated thread.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading;

                    using var done = new ManualResetEventSlim(false);

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Console.WriteLine($"Running on a thread-pool thread (ManagedThreadId {Environment.CurrentManagedThreadId}).");
                        done.Set();
                    });

                    done.Wait(); // ThreadPool work has no built-in "Join" — synchronize manually if you need to wait
                    Console.WriteLine("Thread-pool work item completed.");
                    """
                },
                new()
                {
                    Id = "snip_learn_threading_race_condition",
                    Title = "A Race Condition Without Locking",
                    Description = "Two tasks increment the same counter with no synchronization — updates get lost.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading.Tasks;

                    int counter = 0;

                    void IncrementManyTimes()
                    {
                        for (int i = 0; i < 100_000; i++)
                        {
                            counter++; // NOT atomic: read, add 1, write back — two threads can interleave here
                        }
                    }

                    var t1 = Task.Run(IncrementManyTimes);
                    var t2 = Task.Run(IncrementManyTimes);
                    await Task.WhenAll(t1, t2);

                    Console.WriteLine($"Expected 200,000, got {counter:N0} (likely lower — lost updates from the race condition).");
                    """
                },
                new()
                {
                    Id = "snip_learn_threading_lock_fix",
                    Title = "Fixing It with lock",
                    Description = "The same race condition, corrected with a lock around the shared counter.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading.Tasks;

                    int counter = 0;
                    object counterLock = new(); // a private, dedicated lock object — never lock on "this" or a public object

                    void IncrementManyTimes()
                    {
                        for (int i = 0; i < 100_000; i++)
                        {
                            lock (counterLock)
                            {
                                counter++;
                            }
                        }
                    }

                    var t1 = Task.Run(IncrementManyTimes);
                    var t2 = Task.Run(IncrementManyTimes);
                    await Task.WhenAll(t1, t2);

                    Console.WriteLine($"Expected 200,000, got {counter:N0} (correct — the lock made the increment atomic).");
                    """
                },
                new()
                {
                    Id = "snip_learn_threading_interlocked",
                    Title = "Atomic Counters with Interlocked",
                    Description = "A lighter-weight alternative to lock for simple numeric operations like counters.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading;
                    using System.Threading.Tasks;

                    int counter = 0;

                    void IncrementManyTimes()
                    {
                        for (int i = 0; i < 100_000; i++)
                        {
                            Interlocked.Increment(ref counter);
                        }
                    }

                    var t1 = Task.Run(IncrementManyTimes);
                    var t2 = Task.Run(IncrementManyTimes);
                    await Task.WhenAll(t1, t2);

                    Console.WriteLine($"Expected 200,000, got {counter:N0} — Interlocked avoids the overhead of a full lock for simple cases like this.");
                    """
                },
                new()
                {
                    Id = "snip_learn_threading_cancellation",
                    Title = "Cooperative Cancellation with CancellationToken",
                    Description = "Stop a running loop early and cleanly using a CancellationToken.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading;
                    using System.Threading.Tasks;

                    async Task CountUpAsync(CancellationToken token)
                    {
                        for (int i = 1; i <= 20; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            Console.WriteLine($"Tick {i}");
                            await Task.Delay(150, token);
                        }
                    }

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)); // cancel automatically after 1 second

                    try
                    {
                        await CountUpAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Cancelled — the loop stopped cooperatively instead of running to completion.");
                    }
                    """
                }
            }
        };
    }
}
