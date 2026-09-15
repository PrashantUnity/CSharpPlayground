using System.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Process-wide gate serializing every swap of the real, static <see cref="System.Console"/>
/// Out/Error writers. Both <see cref="ScriptExecutionEngine"/> (Code Studio "Program" runs) and
/// <see cref="NotebookExecutionKernel"/> (notebook cells) redirect the same process-global Console —
/// running one of each concurrently without this gate lets their swap/restore sequences interleave,
/// misattributing output or restoring the wrong "original" writer mid-flight of the other.
/// AsyncLocal cannot fix this: user script code calls the real static Console.WriteLine, which has no
/// flow-awareness, so the only correct fix is serializing the whole redirect-execute-restore span.
/// </summary>
internal static class ConsoleRedirectionGate
{
    internal static readonly SemaphoreSlim Gate = new(1, 1);
}
