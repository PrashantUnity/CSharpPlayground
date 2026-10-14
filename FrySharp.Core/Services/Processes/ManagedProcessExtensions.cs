namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

public static class ManagedProcessExtensions
{
    /// <summary>
    /// Waits for the process to end and returns its exit code. If <paramref name="ct"/> is cancelled first, the process is killed and the
    /// cancellation is thrown. Killing here, where the wait is cancelled, is what makes stopping reliable: a kill registered as a
    /// callback on the token can be disposed by the cancelled wait's own continuation before it has run, which leaves the process going.
    /// </summary>
    public static async Task<int> WaitForExitOrKillAsync(this IManagedProcess process, CancellationToken ct)
    {
        try
        {
            return await process.Completion.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw;
        }
    }
}
