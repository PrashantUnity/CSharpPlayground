using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocArticle CreateAsyncArticle()
    {
        return new DocArticle
        {
            Id = "learn_sync_async",
            Title = "Sync vs Async: Task, await & the .Result Trap",
            Subtitle = "Write responsive, non-blocking C# — and understand exactly why blocking on async code is dangerous.",
            ReadingTime = "6 min read",
            Summary = "async/await lets a method wait on slow work (a web call, a disk read) without blocking the thread it's running on. This chapter covers the fundamentals, running work concurrently, and the classic .Result deadlock pitfall.",
            Keywords = new List<string> { "async", "await", "task", "task.run", "task.whenall", "async void", "deadlock", ".result", ".wait", "configureawait", "synchronizationcontext" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "async/await Fundamentals",
                    Content = "A method marked async and returning Task or Task<T> can use await to suspend at a slow operation without blocking its calling thread. The caller gets a Task back immediately and can await it, or run other work in the meantime.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Name async methods with an Async suffix by convention (FetchDataAsync, not FetchData) so callers can tell at a glance that they should be awaited."
                },
                new()
                {
                    Heading = "async void — Reserve It for Event Handlers",
                    Content = "An async void method has no Task for its caller to await, which means the caller cannot wait for it to finish and cannot catch any exception it throws — an unhandled exception from async void can crash the entire process. Prefer async Task everywhere except UI event handlers, which are required to be void."
                },
                new()
                {
                    Heading = "Task.Run for CPU-Bound Work",
                    Content = "Naturally-async operations (HttpClient calls, File.*Async) already return a Task without extra help. Task.Run is for a different job: offloading actual CPU-heavy work (a big loop, image processing) onto a thread-pool thread so it can run concurrently with other work."
                },
                new()
                {
                    Heading = "Running Work Concurrently with Task.WhenAll",
                    Content = "Starting several tasks first and awaiting them together with Task.WhenAll lets independent operations run concurrently, rather than one after another."
                },
                new()
                {
                    Heading = "The .Result / .Wait() Deadlock Pitfall",
                    Content = "Blocking on async code with .Result or .Wait() is a classic source of deadlocks in UI apps (WPF, WinForms) and classic ASP.NET: the awaited continuation needs to resume on the very same thread that .Result is currently blocking, so neither can proceed.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "The snippet below runs safely in this Code Studio because scripts execute on a background thread pool thread with no captured UI SynchronizationContext to block on — verified by this project's own test suite. The identical code deadlocks forever in a WPF button handler or a classic ASP.NET action. Prefer \"async all the way\" (await instead of .Result/.Wait()) everywhere."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Task.Run", ReturnType = "Task<TResult>", Parameters = "Func<TResult> function", Description = "Queues the given work to run on a thread-pool thread." },
                new() { MethodName = "Task.WhenAll", ReturnType = "Task", Parameters = "params Task[] tasks", Description = "Completes once every one of the supplied tasks has completed." },
                new() { MethodName = "ConfigureAwait", ReturnType = "ConfiguredTaskAwaitable", Parameters = "bool continueOnCapturedContext", Description = "Controls whether the continuation after await must resume on the originally captured context. Use false in library code." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_async_basics",
                    Title = "Basic async/await Method",
                    Description = "An async method that returns a value through Task<T>.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading.Tasks;

                    async Task<int> SquareAsync(int number)
                    {
                        await Task.Delay(200); // simulate a small amount of async work
                        return number * number;
                    }

                    int result = await SquareAsync(9);
                    Console.WriteLine($"9 squared is {result}");
                    """
                },
                new()
                {
                    Id = "snip_learn_async_void_pitfall",
                    Title = "async void vs async Task",
                    Description = "See the structural difference: only async Task gives the caller something to await.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Threading.Tasks;

                    async void FireAndForget(string label)
                    {
                        await Task.Delay(50);
                        Console.WriteLine($"{label}: async void method finished — the caller has no Task to await or observe.");
                    }

                    async Task<string> AwaitableWorkAsync(string label)
                    {
                        await Task.Delay(50);
                        return $"{label}: async Task method finished — the caller CAN await this and catch any exception.";
                    }

                    FireAndForget("Fire-and-forget");
                    Console.WriteLine(await AwaitableWorkAsync("Awaited"));

                    // Give the fire-and-forget call a moment to finish printing before the script ends.
                    await Task.Delay(100);
                    """
                },
                new()
                {
                    Id = "snip_learn_async_task_run",
                    Title = "Task.Run for CPU-Bound Work",
                    Description = "Offload a CPU-heavy loop to the thread pool instead of running it inline.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Diagnostics;
                    using System.Threading.Tasks;

                    static int CountPrimesUpTo(int max)
                    {
                        int count = 0;
                        for (int n = 2; n <= max; n++)
                        {
                            bool isPrime = true;
                            for (int d = 2; d * d <= n; d++)
                            {
                                if (n % d == 0) { isPrime = false; break; }
                            }
                            if (isPrime) count++;
                        }
                        return count;
                    }

                    var stopwatch = Stopwatch.StartNew();

                    int primeCount = await Task.Run(() => CountPrimesUpTo(2_000_000));

                    stopwatch.Stop();
                    Console.WriteLine($"Found {primeCount:N0} primes below 2,000,000 in {stopwatch.ElapsedMilliseconds:N0} ms.");
                    """
                },
                new()
                {
                    Id = "snip_learn_async_whenall",
                    Title = "Task.WhenAll — Run Requests Concurrently",
                    Description = "Start multiple async calls before awaiting any of them, so they run at the same time.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Diagnostics;
                    using System.Net.Http;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    var urls = new[]
                    {
                        "https://jsonplaceholder.typicode.com/posts/1",
                        "https://jsonplaceholder.typicode.com/posts/2",
                        "https://jsonplaceholder.typicode.com/posts/3"
                    };

                    var stopwatch = Stopwatch.StartNew();

                    Task<string>[] downloads = Array.ConvertAll(urls, url => client.GetStringAsync(url));
                    string[] results = await Task.WhenAll(downloads);

                    stopwatch.Stop();
                    Console.WriteLine($"Fetched {results.Length} posts concurrently in {stopwatch.ElapsedMilliseconds:N0} ms.");
                    """
                },
                new()
                {
                    Id = "snip_learn_async_result_pitfall",
                    Title = "The Deadlock-Prone Pattern (.Result)",
                    Description = "Blocking on an async call with .Result — safe here, dangerous in a UI or classic ASP.NET app.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Net.Http;
                    using System.Threading.Tasks;

                    Task<string> FetchTitleAsync()
                    {
                        var client = new HttpClient();
                        return client.GetStringAsync("https://jsonplaceholder.typicode.com/posts/1");
                    }

                    // Blocking on async code with .Result classically deadlocks in WPF/WinForms event handlers
                    // and classic ASP.NET request handlers: the awaited continuation needs to resume on the
                    // very same thread that .Result is currently blocking.
                    string json = FetchTitleAsync().Result;

                    Console.WriteLine("Completed without deadlocking — this host runs scripts on a background");
                    Console.WriteLine("thread-pool thread with no captured UI SynchronizationContext to block on.");
                    Console.WriteLine(json);
                    """
                },
                new()
                {
                    Id = "snip_learn_async_safe_fix",
                    Title = "The Safe Fix (await All the Way)",
                    Description = "The corrected version of the previous snippet: await instead of blocking, using ConfigureAwait(false) as a library would.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Net.Http;
                    using System.Threading.Tasks;

                    async Task<string> FetchTitleAsync()
                    {
                        var client = new HttpClient();
                        return await client.GetStringAsync("https://jsonplaceholder.typicode.com/posts/1").ConfigureAwait(false);
                    }

                    // "Async all the way": await the call instead of blocking on .Result or .Wait().
                    // ConfigureAwait(false) tells the continuation it does not need to resume on any
                    // particular captured context — the recommended default for library/non-UI code.
                    string json = await FetchTitleAsync();

                    Console.WriteLine(json);
                    """
                }
            }
        };
    }
}
