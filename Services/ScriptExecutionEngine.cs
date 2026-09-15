using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class ExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public TimeSpan Elapsed { get; set; }
    public bool WasCancelled { get; set; }
}

public class ScriptExecutionEngine
{
    public async Task<ExecutionResult> ExecuteAsync(
        byte[] assemblyBytes,
        Action<string>? onLiveOutput = null,
        CancellationToken ct = default)
    {
        var result = new ExecutionResult();
        var sw = Stopwatch.StartNew();

        // Console.Out/Error are process-wide statics — serialize the swap against every other
        // execution engine in the process (notably NotebookExecutionKernel), or a concurrent
        // notebook cell run and Code Studio run can misattribute output / restore the wrong writer.
        // Everything from here on is inside the outer try so the gate is released exactly once,
        // even if something between acquiring it and starting execution throws.
        await ConsoleRedirectionGate.Gate.WaitAsync(ct);
        try
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;

            var liveWriter = new LiveStringWriter(text =>
            {
                onLiveOutput?.Invoke(text);
            });

            Console.SetOut(liveWriter);
            Console.SetError(liveWriter);

            var context = new CollectibleAssemblyLoadContext();

            try
            {
                await Task.Run(() =>
                {
                    using var ms = new MemoryStream(assemblyBytes);
                    var assembly = context.LoadFromStream(ms);

                    var entryPoint = assembly.EntryPoint;
                    if (entryPoint == null)
                    {
                        throw new InvalidOperationException("No entry point found. Please declare 'public static void Main()' or 'public static async Task Main()'.");
                    }

                    var parameters = entryPoint.GetParameters();
                    object?[]? args = null;
                    if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string[]))
                    {
                        args = [Array.Empty<string>()];
                    }

                    ct.ThrowIfCancellationRequested();

                    var returnVal = entryPoint.Invoke(null, args);
                    if (returnVal is Task task)
                    {
                        task.GetAwaiter().GetResult();
                    }
                }, ct);

                result.Success = true;
            }
            catch (OperationCanceledException)
            {
                result.WasCancelled = true;
                liveWriter.WriteLine("\n⚠️ Execution was cancelled by user or timed out.");
            }
            catch (TargetInvocationException tie)
            {
                result.Success = false;
                var inner = tie.InnerException ?? tie;
                result.Error = inner.Message;
                liveWriter.WriteLine($"\n❌ Runtime Exception: {inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                liveWriter.WriteLine($"\n❌ Error: {ex.Message}");
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                sw.Stop();

                result.Elapsed = sw.Elapsed;
                result.Output = liveWriter.ToString();

                context.Unload();
            }
        }
        finally
        {
            ConsoleRedirectionGate.Gate.Release();
        }

        return result;
    }

    private sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext() : base(isCollectible: true) { }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name != null && string.Equals(assemblyName.Name, typeof(ScriptExecutionEngine).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(ScriptExecutionEngine).Assembly;
            }
            return null; // Fallback to default load context
        }
    }

    private sealed class LiveStringWriter : StringWriter
    {
        private readonly Action<string> _onWrite;

        public LiveStringWriter(Action<string> onWrite)
        {
            _onWrite = onWrite;
        }

        public override void Write(char value)
        {
            base.Write(value);
            _onWrite(value.ToString());
        }

        public override void Write(string? value)
        {
            base.Write(value);
            if (value != null)
            {
                _onWrite(value);
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            if (buffer != null && count > 0)
            {
                _onWrite(new string(buffer, index, count));
            }
        }
    }
}
