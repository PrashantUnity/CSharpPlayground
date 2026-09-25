using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Finds the XML documentation (IntelliSense) files for the assemblies scripts reference, so metadata symbols get their
/// summaries: <c>Foo.xml</c> next to <c>Foo.dll</c> (NuGet packages, the plugin itself), the SDK's reference pack for
/// .NET (the runtime folder the compiler references ships none), then the NuGet cache for host assemblies such as Avalonia.
/// Files are read lazily, once, and kept.
/// </summary>
public static class XmlDocumentationLookup
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        XmlResolver = null
    };

    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>> Files = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> AssemblyFiles = new(StringComparer.Ordinal);
    private static readonly Lazy<string?> ReferencePackFolder = new(FindReferencePackFolder);
    private static readonly Lazy<IReadOnlyDictionary<string, string>> FrameworkTypeIndex = new(BuildFrameworkTypeIndex);
    private static readonly string RuntimeFolder = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;

    /// <summary>
    /// The same references (sharing their loaded metadata) with a documentation provider attached, so
    /// <see cref="ISymbol.GetDocumentationCommentXml"/> works for the symbols they define.
    /// </summary>
    public static IReadOnlyList<MetadataReference> WithDocumentation(IEnumerable<MetadataReference> references) =>
        references.Select(WithDocumentation).ToList();

    /// <summary>The documentation XML for one member (<c>M:System.Console.WriteLine(System.String)</c>) of the assembly at <paramref name="assemblyPath"/>.</summary>
    public static string? FindDocumentation(string assemblyPath, string documentationId)
    {
        foreach (var file in FilesFor(assemblyPath))
        {
            if (Load(file).TryGetValue(documentationId, out var xml)) return xml;
        }

        // .NET implements many types in a different assembly from the one documenting them: List<T> lives in
        // System.Private.CoreLib but is documented in System.Collections.xml. Find the file by the member's type.
        if (IsFrameworkAssembly(assemblyPath) && FrameworkFileFor(documentationId) is { } frameworkFile &&
            Load(frameworkFile).TryGetValue(documentationId, out var frameworkXml))
        {
            return frameworkXml;
        }

        return null;
    }

    /// <summary>"M:System.Collections.Generic.List`1.Add(`0)" → "T:System.Collections.Generic.List`1".</summary>
    public static string? ContainingTypeId(string documentationId)
    {
        if (documentationId.Length < 3 || documentationId[1] != ':') return null;
        if (documentationId[0] == 'T') return documentationId;
        if (documentationId[0] is 'N' or '!') return null;

        var name = documentationId[2..];
        var end = name.IndexOfAny(['(', '~']);
        if (end >= 0) name = name[..end];

        var dot = name.LastIndexOf('.');
        return dot > 0 ? "T:" + name[..dot] : null;
    }

    private static MetadataReference WithDocumentation(MetadataReference reference)
    {
        if (reference is not PortableExecutableReference { FilePath: { Length: > 0 } path } portable) return reference;

        try
        {
            return portable.GetMetadata() is AssemblyMetadata metadata
                ? metadata.GetReference(
                    documentation: new AssemblyDocumentationProvider(path),
                    aliases: portable.Properties.Aliases,
                    embedInteropTypes: portable.Properties.EmbedInteropTypes,
                    filePath: path,
                    display: portable.Display)
                : reference;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            return reference;
        }
    }

    private static IReadOnlyList<string> FilesFor(string assemblyPath) => AssemblyFiles.GetOrAdd(assemblyPath, path =>
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var files = new List<string>();

        var sibling = Path.ChangeExtension(path, ".xml");
        if (File.Exists(sibling)) files.Add(sibling);

        if (IsFrameworkAssembly(path))
        {
            var framework = ReferencePackFolder.Value is { } referencePack ? Path.Combine(referencePack, name + ".xml") : null;
            if (File.Exists(framework)) files.Add(framework);
        }
        else if (files.Count == 0 && FindInNuGetCache(path, name) is { } packaged)
        {
            files.Add(packaged);
        }

        return files;
    });

    private static bool IsFrameworkAssembly(string assemblyPath) =>
        string.Equals(Path.GetDirectoryName(assemblyPath), RuntimeFolder, StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, string> Load(string xmlPath) =>
        Files.GetOrAdd(xmlPath, path => new Lazy<IReadOnlyDictionary<string, string>>(() => ReadMembers(path))).Value;

    // <doc><members><member name="M:...">inner XML</member>...</members></doc> → name → inner XML, as Roslyn's own provider reads it.
    private static IReadOnlyDictionary<string, string> ReadMembers(string xmlPath)
    {
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var reader = XmlReader.Create(xmlPath, ReaderSettings);
            while (reader.ReadToFollowing("member"))
            {
                if (reader.GetAttribute("name") is { Length: > 0 } name)
                {
                    members[name] = reader.ReadInnerXml();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            Debug.WriteLine($"[XmlDocumentationLookup] Could not read {xmlPath}: {ex.Message}");
        }
        return members;
    }

    private static string? FrameworkFileFor(string documentationId)
    {
        var index = FrameworkTypeIndex.Value;
        // A nested type's members name it with dots too ("F:System.Environment.SpecialFolder.Desktop"), so walk outwards.
        for (var typeId = ContainingTypeId(documentationId); typeId != null; typeId = ContainingTypeId("M:" + typeId[2..]))
        {
            if (index.TryGetValue(typeId, out var file)) return file;
        }
        return null;
    }

    // Every type the reference pack documents ("T:System.String" → .../System.Runtime.xml). Built once, on first need,
    // by streaming the files without keeping their text (about 30 MB of XML, a few hundred milliseconds).
    private static IReadOnlyDictionary<string, string> BuildFrameworkTypeIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ReferencePackFolder.Value is not { } folder) return index;

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.xml");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return index;
        }

        foreach (var file in files)
        {
            try
            {
                using var reader = XmlReader.Create(file, ReaderSettings);
                while (reader.ReadToFollowing("member"))
                {
                    if (reader.GetAttribute("name") is { } name && name.StartsWith("T:", StringComparison.Ordinal))
                    {
                        index.TryAdd(name, file);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
            {
                Debug.WriteLine($"[XmlDocumentationLookup] Could not index {file}: {ex.Message}");
            }
        }
        return index;
    }

    // <dotnet>/packs/Microsoft.NETCore.App.Ref/<version>/ref/net<major>.<minor>, which only an installed SDK has.
    // Lazy<T> would keep an exception forever, so a folder that can't be read just means no .NET documentation.
    private static string? FindReferencePackFolder()
    {
        try
        {
            return FindReferencePackFolderIn(DotnetRoots());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Debug.WriteLine($"[XmlDocumentationLookup] No .NET reference pack: {ex.Message}");
            return null;
        }
    }

    private static List<string> DotnetRoots()
    {
        var roots = new List<string>();
        // <dotnet>/shared/Microsoft.NETCore.App/<version> is where the running runtime lives.
        if (RuntimeFolder.Length > 0 && new DirectoryInfo(RuntimeFolder).Parent?.Parent?.Parent is { } dotnetRoot) roots.Add(dotnetRoot.FullName);
        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } environmentRoot) roots.Add(environmentRoot);
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"));
        if (OperatingSystem.IsWindows())
        {
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"));
        }
        else
        {
            roots.AddRange(["/usr/local/share/dotnet", "/usr/share/dotnet", "/usr/lib/dotnet"]);
        }
        return roots;
    }

    private static string? FindReferencePackFolderIn(IEnumerable<string> roots)
    {
        var running = Environment.Version;
        foreach (var root in roots.Distinct(StringComparer.Ordinal))
        {
            var packs = Path.Combine(root, "packs", "Microsoft.NETCore.App.Ref");
            if (!Directory.Exists(packs)) continue;

            // The running version first, then the newest of the same major.minor, then the newest of all.
            var versions = Directory.GetDirectories(packs)
                .Select(folder => (Folder: folder, Version: ParseVersion(Path.GetFileName(folder))))
                .Where(v => v.Version != null)
                .OrderByDescending(v => v.Version == running)
                .ThenByDescending(v => v.Version!.Major == running.Major && v.Version.Minor == running.Minor)
                .ThenByDescending(v => v.Version);

            foreach (var (folder, version) in versions)
            {
                var reference = Path.Combine(folder, "ref", $"net{version!.Major}.{version.Minor}");
                if (Directory.Exists(reference) && Directory.EnumerateFiles(reference, "*.xml").Any()) return reference;
            }
        }
        return null;
    }

    // ~/.nuget/packages/<id>/<version>/lib/<tfm>/<Assembly>.xml. The id is the assembly name or a dotted prefix of it
    // (Avalonia.Base.dll ships in the "avalonia" package).
    private static string? FindInNuGetCache(string assemblyPath, string assemblyName)
    {
        var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        if (!Directory.Exists(packages)) return null;

        try
        {
            var version = PackageVersionOf(assemblyPath);
            var parts = assemblyName.Split('.');
            for (var count = parts.Length; count >= 1; count--)
            {
                var package = Path.Combine(packages, string.Join('.', parts.Take(count)).ToLowerInvariant());
                if (!Directory.Exists(package)) continue;

                var versionFolder = version != null && Directory.Exists(Path.Combine(package, version))
                    ? Path.Combine(package, version)
                    : Directory.GetDirectories(package).OrderByDescending(f => ParseVersion(Path.GetFileName(f))).FirstOrDefault();
                var lib = versionFolder == null ? null : Path.Combine(versionFolder, "lib");
                if (lib == null || !Directory.Exists(lib)) continue;

                var xml = Directory.EnumerateDirectories(lib)
                    .Select(tfm => Path.Combine(tfm, assemblyName + ".xml"))
                    .FirstOrDefault(File.Exists);
                if (xml != null) return xml;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[XmlDocumentationLookup] NuGet cache lookup failed for {assemblyName}: {ex.Message}");
        }
        return null;
    }

    // "12.1.2+3f2a..." → "12.1.2": the package folder name.
    private static string? PackageVersionOf(string assemblyPath)
    {
        try
        {
            var product = FileVersionInfo.GetVersionInfo(assemblyPath).ProductVersion;
            if (!string.IsNullOrWhiteSpace(product)) return product.Split('+')[0].Trim();
            return AssemblyName.GetAssemblyName(assemblyPath).Version?.ToString(3);
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or ArgumentException)
        {
            return null;
        }
    }

    private static Version? ParseVersion(string text) =>
        Version.TryParse(text.Split('-', '+')[0], out var version) ? version : null;

    /// <summary>Looks documentation up lazily when Roslyn asks for a symbol's comment.</summary>
    private sealed class AssemblyDocumentationProvider(string assemblyPath) : DocumentationProvider
    {
        private readonly string _assemblyPath = assemblyPath;

        protected override string? GetDocumentationForSymbol(
            string documentationMemberID,
            CultureInfo preferredCulture,
            CancellationToken cancellationToken = default) =>
            FindDocumentation(_assemblyPath, documentationMemberID);

        // Roslyn only shares a compiled assembly's symbols between compilations whose providers are equal.
        public override bool Equals(object? obj) =>
            obj is AssemblyDocumentationProvider other && string.Equals(other._assemblyPath, _assemblyPath, StringComparison.Ordinal);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_assemblyPath);
    }
}
