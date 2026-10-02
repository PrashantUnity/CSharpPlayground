using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildAsyncParallelCategory()
    {
        return new DocCategory
        {
            Id = "async_parallel",
            Title = "Asynchronous & Parallel Programming",
            IconKind = MaterialIconKind.SyncCircle,
            AccentColor = "#2DD4BF",
            Badge = "Concurrency",
            Description = "Task-based async, the Task Parallel Library, thread safety, and avoiding deadlocks.",
            Articles = new List<DocArticle>
            {
                CreateAsyncParallelArticle()
            }
        };
    }

    private DocArticle CreateAsyncParallelArticle()
    {
        return new DocArticle
        {
            Id = "learn_async_parallel",
            Title = "Async & Parallel: TPL, Parallel.For & Synchronization",
            Subtitle = "Go beyond a single await: run CPU-bound work in parallel, protect shared state, and avoid the deadlocks that come with mixing the two.",
            ReadingTime = "8 min read",
            Summary = "async/await is about not blocking a thread while you wait on something slow. The Task Parallel Library is about a different problem: using every available core to finish CPU-bound work faster. This chapter shows both, how to keep shared state safe when several threads touch it at once, and the classic ways concurrent code deadlocks.",
            Keywords = new List<string> { "task parallel library", "tpl", "parallel.for", "parallel.foreach", "task vs thread", "thread safety", "lock", "monitor", "semaphoreslim", "deadlock", "async await" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "async/await: A Quick Refresher",
                    Content = "await suspends an async method at a slow, naturally-asynchronous operation (a network call, a disk read) without occupying a thread while it waits. The method's continuation resumes later, often on a different thread-pool thread, when the awaited Task completes. This is concurrency through waiting, not through using more CPU cores — a single await never makes your code run in parallel.",
                    BulletPoints = new List<string>
                    {
                        "await frees the current thread while waiting — it does not create a new thread.",
                        "Task and Task<T> represent the eventual result (or exception) of that work.",
                        "This section builds on the basic async/await chapter — see it for async void, .Result, and ConfigureAwait."
                    }
                },
                new()
                {
                    Heading = "Task vs Thread",
                    Content = "A Thread is an OS-level construct: creating one reserves roughly a megabyte of stack and a kernel scheduling context, which is expensive to spin up for short-lived work. A Task is a much lighter abstraction — a unit of work that either runs on a pooled thread pool thread (via Task.Run) or represents naturally-async I/O that may not occupy any thread at all while it's in flight.",
                    BulletPoints = new List<string>
                    {
                        "Thread — use for a small number of long-lived, dedicated background workers.",
                        "Task.Run(...) — use to offload short-lived, CPU-bound work onto the shared thread pool.",
                        "A plain Task returned by an I/O API (HttpClient, File.*Async) usually uses no thread at all while awaited — it's driven by OS-level completion callbacks."
                    }
                },
                new()
                {
                    Heading = "The Task Parallel Library (TPL)",
                    Content = "The Task Parallel Library, in System.Threading.Tasks, is the framework's toolkit for data parallelism and task parallelism: splitting one big CPU-bound job across all available cores. Parallel.For and Parallel.ForEach are its most common entry points — they partition a loop's iterations across the thread pool automatically, so you don't have to manage threads or chunking by hand.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Reach for the TPL only when the work is CPU-bound and independent per iteration. Parallelizing I/O-bound work (web calls, file reads) is almost always better served by Task.WhenAll with async APIs than by Parallel.For."
                },
                new()
                {
                    Heading = "Parallel.For and Parallel.ForEach",
                    Content = "Parallel.For(fromInclusive, toExclusive, body) and Parallel.ForEach(source, body) run loop iterations concurrently across multiple threads, using a Partitioner internally to divide the work into batches sized for the number of available cores. Each call blocks the calling thread until every iteration has completed (or one throws).",
                    BulletPoints = new List<string>
                    {
                        "Iterations must be independent — no iteration should depend on another having already run.",
                        "The loop body runs on multiple threads at once, so any shared state it touches needs the same protection as any other multi-threaded code.",
                        "Both methods return a ParallelLoopResult and accept a ParallelOptions to cap MaxDegreeOfParallelism or supply a CancellationToken."
                    }
                },
                new()
                {
                    Heading = "Thread Safety",
                    Content = "Code is thread-safe when multiple threads can call it concurrently without corrupting shared state or producing inconsistent results. Local variables inside a method are always safe — each call gets its own copy on its own stack. The danger is state that outlives a single call and is reachable from more than one thread at once: fields, static variables, and objects passed into Parallel.For/ForEach bodies or Task.Run delegates.",
                    BulletPoints = new List<string>
                    {
                        "Prefer immutable data and per-iteration local state wherever possible — it needs no synchronization at all.",
                        "When mutable shared state is unavoidable, either use the framework's concurrent collections (ConcurrentDictionary, ConcurrentBag) or protect access explicitly with lock/Monitor/SemaphoreSlim.",
                        "A method that only reads shared state, never writes it, is safe to call concurrently without locking."
                    }
                },
                new()
                {
                    Heading = "lock, Monitor & SemaphoreSlim",
                    Content = "lock (which compiles to Monitor.Enter/Monitor.Exit in a try/finally) lets only one thread at a time execute a block of code — the right tool for protecting synchronous, in-memory shared state such as a counter or a shared collection. It cannot be awaited inside, though, so it doesn't work for guarding an async critical section. SemaphoreSlim.WaitAsync fills that gap: it's an awaitable gate that lets up to a configured number of callers in at once (one, for a mutual-exclusion lock), and it composes cleanly with await.",
                    BulletPoints = new List<string>
                    {
                        "lock (Monitor) — synchronous, fast, cannot contain an await.",
                        "SemaphoreSlim(1, 1) with WaitAsync/Release — the async-friendly equivalent of a mutual-exclusion lock.",
                        "SemaphoreSlim with a higher initial count also works as a concurrency limiter — e.g. capping how many downloads run at once."
                    }
                },
                new()
                {
                    Heading = "Deadlocks & Best Practices",
                    Content = "A deadlock happens when two or more threads each hold a resource the other needs and neither can proceed. In concurrent C#, the two classic causes are blocking on async code with .Result/.Wait() (the continuation needs the very thread that's blocked waiting for it) and acquiring the same set of locks in different orders on different threads.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Two rules avoid almost every deadlock: (1) never call .Result or .Wait() on a Task from code that might run on a context with a limited thread pool or a captured SynchronizationContext — await it instead, all the way up the call stack; (2) if a code path ever needs to hold more than one lock at a time, always acquire them in the same, fixed order everywhere in the codebase."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Task.Run", ReturnType = "Task<TResult>", Parameters = "Func<TResult> function", Description = "Queues CPU-bound work onto a thread-pool thread and returns a Task representing its completion." },
                new() { MethodName = "Parallel.For", ReturnType = "ParallelLoopResult", Parameters = "int fromInclusive, int toExclusive, Action<int> body", Description = "Runs the loop body for each index in the range, spread across multiple threads, blocking until all iterations finish." },
                new() { MethodName = "Parallel.ForEach", ReturnType = "ParallelLoopResult", Parameters = "IEnumerable<TSource> source, Action<TSource> body", Description = "Runs the loop body once per element of source, spread across multiple threads, blocking until all iterations finish." },
                new() { MethodName = "SemaphoreSlim.WaitAsync", ReturnType = "Task", Parameters = "", Description = "Asynchronously waits to enter the semaphore, without blocking the calling thread while it waits." },
                new() { MethodName = "Monitor.Enter / Monitor.Exit", ReturnType = "void", Parameters = "object obj", Description = "What the lock keyword compiles to — acquires and releases an exclusive lock on obj." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_async_parallel_parallel_foreach",
                    Title = "Parallel.ForEach for CPU-Bound Work",
                    Description = "Sum the digit-counts of many numbers across all available cores instead of one at a time.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Concurrent;
                    using System.Diagnostics;
                    using System.Linq;
                    using System.Threading.Tasks;

                    static bool IsPrime(int n)
                    {
                        if (n < 2) return false;
                        for (int d = 2; d * d <= n; d++)
                        {
                            if (n % d == 0) return false;
                        }
                        return true;
                    }

                    var numbers = Enumerable.Range(2, 500_000).ToArray();
                    var primeBag = new ConcurrentBag<int>(); // thread-safe: many threads add() concurrently

                    var stopwatch = Stopwatch.StartNew();

                    Parallel.ForEach(numbers, n =>
                    {
                        if (IsPrime(n))
                        {
                            primeBag.Add(n);
                        }
                    });

                    stopwatch.Stop();
                    Console.WriteLine($"Found {primeBag.Count:N0} primes in {stopwatch.ElapsedMilliseconds:N0} ms using Parallel.ForEach.");
                    """
                },
                new()
                {
                    Id = "snip_learn_async_parallel_semaphore",
                    Title = "SemaphoreSlim: An Async-Friendly Critical Section",
                    Description = "Guard a shared resource across concurrent async calls without blocking any thread while waiting.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading;
                    using System.Threading.Tasks;

                    var gate = new SemaphoreSlim(1, 1); // acts as a mutual-exclusion lock, but is awaitable
                    int sharedTotal = 0;

                    async Task AddToTotalAsync(int workerId, int amount)
                    {
                        await gate.WaitAsync(); // asynchronously waits for exclusive access — no thread is blocked while waiting
                        try
                        {
                            int before = sharedTotal;
                            await Task.Delay(10); // simulate a bit of async work while "inside" the critical section
                            sharedTotal = before + amount;
                            Console.WriteLine($"Worker {workerId} added {amount}, total is now {sharedTotal}.");
                        }
                        finally
                        {
                            gate.Release(); // always release, even if the body throws
                        }
                    }

                    var workers = new Task[5];
                    for (int i = 0; i < workers.Length; i++)
                    {
                        int id = i;
                        workers[i] = AddToTotalAsync(id, 10);
                    }

                    await Task.WhenAll(workers);
                    Console.WriteLine($"Final total: {sharedTotal} (expected 50 — the semaphore prevented lost updates).");
                    """
                },
                new()
                {
                    Id = "snip_learn_async_parallel_deadlock_vs_fix",
                    Title = "A Lock-Ordering Deadlock, and the Fix",
                    Description = "Two tasks acquire the same two locks in opposite order and can deadlock; acquiring them in a consistent order fixes it.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading;
                    using System.Threading.Tasks;

                    object lockA = new();
                    object lockB = new();

                    // DEADLOCK-PRONE: each task grabs the two locks in a different order.
                    // Task 1 can hold lockA while waiting for lockB at the same moment
                    // Task 2 holds lockB while waiting for lockA — neither can ever proceed.
                    void RiskyTransferAToB()
                    {
                        lock (lockA)
                        {
                            Thread.Sleep(50); // widen the window so the race is easy to reproduce
                            lock (lockB)
                            {
                                Console.WriteLine("RiskyTransferAToB acquired both locks.");
                            }
                        }
                    }

                    void RiskyTransferBToA()
                    {
                        lock (lockB)
                        {
                            Thread.Sleep(50);
                            lock (lockA)
                            {
                                Console.WriteLine("RiskyTransferBToA acquired both locks.");
                            }
                        }
                    }

                    // FIX: always acquire locks in the same fixed order (here, lockA before lockB),
                    // no matter which "direction" the operation conceptually represents.
                    void SafeTransferAToB()
                    {
                        lock (lockA)
                        {
                            lock (lockB)
                            {
                                Console.WriteLine("SafeTransferAToB acquired both locks in a fixed order.");
                            }
                        }
                    }

                    void SafeTransferBToA()
                    {
                        lock (lockA) // note: still lockA first, even though this method is "B to A"
                        {
                            lock (lockB)
                            {
                                Console.WriteLine("SafeTransferBToA acquired both locks in the same fixed order.");
                            }
                        }
                    }

                    // Run the safe versions concurrently — a fixed lock order means they can never deadlock.
                    await Task.WhenAll(
                        Task.Run(SafeTransferAToB),
                        Task.Run(SafeTransferBToA));

                    Console.WriteLine("Both safe transfers completed without deadlocking.");
                    """
                }
            }
        };
    }
}
