using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Storage;

/// <summary>
/// Saves a file so that it is never left half-written: the text goes to a temporary file next to it, which then replaces
/// it in one rename. A crash, a full disk or a second writer leaves the old file or the new one, never a mix. A file that
/// is a symbolic link is written where the link points (the link stays a link), and the file's permissions (an
/// executable script stays executable) are kept.
/// </summary>
public static class AtomicFile
{
    public static async Task WriteAllTextAsync(string path, string text, Encoding encoding, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);

        var target = FinalTarget(path);
        var folder = Path.GetDirectoryName(target) ?? throw new IOException($"'{path}' has no folder.");
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                var preamble = encoding.GetPreamble();
                if (preamble.Length > 0) await stream.WriteAsync(preamble, ct).ConfigureAwait(false);
                await stream.WriteAsync(encoding.GetBytes(text), ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsWindows() && File.Exists(target))
            {
                File.SetUnixFileMode(temp, File.GetUnixFileMode(target));
            }

            File.Move(temp, target, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    // Where a link (or a chain of links) ends; the path itself when it is no link.
    private static string FinalTarget(string path)
    {
        var info = new FileInfo(path);
        return info.LinkTarget != null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target ? target.FullName : path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left behind; it is hidden and named so it is clearly a leftover.
        }
    }
}
