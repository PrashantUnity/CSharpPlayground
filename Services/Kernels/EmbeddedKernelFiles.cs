using System.Reflection;
using System.Security.Cryptography;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// Writes a kernel program that ships inside the plugin (as embedded resources) to a folder it can run from:
/// <c>root/&lt;hash of the files&gt;/</c>, so a new version of the plugin gets a fresh folder and an old one is never
/// half-overwritten while it runs.
/// </summary>
public static class EmbeddedKernelFiles
{
    /// <param name="resourcePrefix">The resources' name prefix, e.g. "PythonKernel."; the rest of each name is its file name.</param>
    /// <returns>The folder holding the files.</returns>
    public static string Extract(Assembly assembly, string resourcePrefix, string root)
    {
        var files = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => (Name: n[resourcePrefix.Length..], Bytes: Read(assembly, n)))
            .ToList();
        if (files.Count == 0) throw new InvalidOperationException($"The plugin has no embedded files named {resourcePrefix}*.");

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (name, bytes) in files)
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(name));
            hash.AppendData(bytes);
        }

        var folder = Path.Combine(root, Convert.ToHexString(hash.GetHashAndReset(), 0, 8).ToLowerInvariant());
        if (files.All(f => File.Exists(Path.Combine(folder, f.Name)))) return folder;

        // Written to a temporary folder, then moved into place in one step: two studios starting at once can't see
        // (or write) half a kernel.
        var staging = folder + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        foreach (var (name, bytes) in files) File.WriteAllBytes(Path.Combine(staging, name), bytes);
        try
        {
            Directory.Move(staging, folder);
        }
        catch (IOException) when (Directory.Exists(folder))
        {
            Directory.Delete(staging, recursive: true); // someone else put it there first
        }

        return folder;
    }

    private static byte[] Read(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException($"Missing resource {resource}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
