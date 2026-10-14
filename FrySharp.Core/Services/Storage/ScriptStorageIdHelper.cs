using System.Security.Cryptography;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Storage;

public static class ScriptStorageIdHelper
{
    public const string SourceFileIdPrefix = "src-";

    public static bool IsSourceFileId(string? id) => id != null && id.StartsWith(SourceFileIdPrefix, StringComparison.Ordinal);

    public static string SourceFileId(string path)
    {
        var full = Path.GetFullPath(path);
        if (!OperatingSystem.IsLinux()) full = full.ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(full));
        return SourceFileIdPrefix + Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
    }
}
