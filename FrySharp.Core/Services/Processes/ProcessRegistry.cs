using System.Diagnostics;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

/// <summary>
/// Every program the studio has started and not yet seen end, so none outlives the studio: the plugin kills them all
/// when it's unloaded and when the app exits.
/// </summary>
public static class ProcessRegistry
{
    private static readonly object Gate = new();
    private static readonly HashSet<IManagedProcess> Live = new();

    public static int LiveCount
    {
        get
        {
            lock (Gate) return Live.Count;
        }
    }

    internal static void Add(IManagedProcess process)
    {
        lock (Gate) Live.Add(process);
    }

    internal static void Remove(IManagedProcess process)
    {
        lock (Gate) Live.Remove(process);
    }

    public static void KillAll()
    {
        IManagedProcess[] all;
        lock (Gate) all = Live.ToArray();
        foreach (var process in all)
        {
            try
            {
                process.Kill();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Couldn't stop process {process.Id}: {ex.Message}");
            }
        }
    }

    /// <summary>Kills every program when the app exits, until the returned handle is disposed.</summary>
    public static IDisposable KillAllOnExit()
    {
        EventHandler handler = (_, _) => KillAll();
        AppDomain.CurrentDomain.ProcessExit += handler;
        return new Unsubscriber(() => AppDomain.CurrentDomain.ProcessExit -= handler);
    }

    private sealed class Unsubscriber(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
