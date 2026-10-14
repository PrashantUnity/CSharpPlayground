using System.Text;
using FrySharp.Sidecar.Hosting;
using FrySharp.Sidecar.Protocol;
using FrySharp.Sidecar.Sessions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using StreamJsonRpc;

namespace FrySharp.Sidecar.Services;

/// <summary>
/// Files opened and saved by the VS Code extension.
/// </summary>
public sealed class DocumentService
{
    private readonly SidecarOptions _options;
    private readonly WorkspaceService _workspace;
    private readonly LanguageFeatures _languageFeatures;

    public DocumentService(SidecarOptions options, WorkspaceService workspace, LanguageFeatures languageFeatures)
    {
        _options = options;
        _workspace = workspace;
        _languageFeatures = languageFeatures;
    }

    public async Task<DocumentContent> OpenAsync(string path, CancellationToken ct)
    {
        var full = _workspace.Resolve(path);
        var info = new FileInfo(full);
        if (!info.Exists) throw Error(ErrorCodes.NotFound, $"'{path}' doesn't exist.");
        if (info.Length > _options.MaxOpenFileBytes)
        {
            throw Error(ErrorCodes.TooLarge, $"'{path}' is {info.Length / 1048576.0:F1} MB; files over {_options.MaxOpenFileBytes / 1048576} MB cannot be opened.");
        }

        var bytes = await File.ReadAllBytesAsync(full, ct).ConfigureAwait(false);
        if (TextFileReader.LooksBinary(bytes)) throw Error(ErrorCodes.NotSupported, $"'{path}' is a binary file.");

        var content = TextFileReader.Decode(bytes);
        return new DocumentContent(_workspace.Paths.ToRelative(full), content.Text, _languageFeatures.LanguageOf(full),
            DiskVersionOf(info), info.IsReadOnly);
    }

    public void DidOpen(SidecarSession session, DidOpenParams request)
    {
        var full = _workspace.Resolve(request.Path);
        var encoding = File.Exists(full) ? EncodingOf(full) : new UTF8Encoding(false);
        var document = new OpenDocument(_workspace.Paths.ToRelative(full), full, request.Text, request.Version, request.DiskVersion, encoding);
        session.Documents[document.Path] = document;
        _languageFeatures.DocumentChanged(session, document);
    }

    public void DidChange(SidecarSession session, DidChangeParams request)
    {
        if (!session.Documents.TryGetValue(request.Path, out var document)) return;

        lock (document.Gate)
        {
            if (document.OutOfSync) return;
            if (request.Version != document.Version + 1)
            {
                document.OutOfSync = true;
                session.Notify(ClientMethods.DocumentResync, new DocumentNotification(document.Path));
                return;
            }

            var text = new StringBuilder(document.Text);
            foreach (var change in request.Changes.OrderByDescending(c => c.Offset))
            {
                if (change.Offset < 0 || change.Length < 0 || change.Offset + change.Length > text.Length)
                {
                    document.OutOfSync = true;
                    session.Notify(ClientMethods.DocumentResync, new DocumentNotification(document.Path));
                    return;
                }
                text.Remove(change.Offset, change.Length).Insert(change.Offset, change.Text);
            }

            document.Text = text.ToString();
            document.Version = request.Version;
        }

        _languageFeatures.DocumentChanged(session, document);
    }

    public void DidClose(SidecarSession session, string path)
    {
        if (session.Documents.TryRemove(path, out var document)) _languageFeatures.Forget(session, document);
    }

    public async Task<SaveResult> SaveAsync(SidecarSession session, SaveParams request, CancellationToken ct)
    {
        if (!session.Documents.TryGetValue(request.Path, out var document)) throw Error(ErrorCodes.NotFound, $"'{request.Path}' is not open.");

        string text;
        Encoding encoding;
        lock (document.Gate)
        {
            if (document.OutOfSync || document.Version != request.Version)
            {
                throw Error(ErrorCodes.OutOfSync, "The sidecar's copy of the document is behind the editor's.");
            }

            var onDisk = File.Exists(document.FullPath) ? DiskVersionOf(new FileInfo(document.FullPath)) : null;
            if (!request.Force && onDisk != null && onDisk != document.DiskVersion)
            {
                throw Error(ErrorCodes.Conflict, $"'{document.Path}' changed on disk since it was opened.");
            }

            text = document.Text;
            encoding = document.Encoding;
        }

        await AtomicFile.WriteAllTextAsync(document.FullPath, text, encoding, ct).ConfigureAwait(false);
        var saved = DiskVersionOf(new FileInfo(document.FullPath));
        lock (document.Gate)
        {
            document.DiskVersion = saved;
            document.ChangeOnDiskReported = false;
        }
        return new SaveResult(saved);
    }

    public IReadOnlyDictionary<string, string> OpenTexts(SidecarSession session) =>
        session.Documents.Values.ToDictionary(
            d => ScriptStorageIdHelper.SourceFileId(d.FullPath),
            d => { lock (d.Gate) return d.Text; });

    public static string DiskVersionOf(FileInfo info) => $"{info.LastWriteTimeUtc.Ticks:x}-{info.Length:x}";

    private static Encoding EncodingOf(string path)
    {
        Span<byte> head = stackalloc byte[3];
        using var stream = File.OpenRead(path);
        var read = stream.Read(head);
        return TextFileReader.Decode(head[..read].ToArray()).Encoding;
    }

    private static LocalRpcException Error(int code, string message) => new(message) { ErrorCode = code };
}
