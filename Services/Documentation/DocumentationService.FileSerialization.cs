using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildFileSerializationCategory()
    {
        return new DocCategory
        {
            Id = "file_serialization",
            Title = "File Handling & Serialization",
            IconKind = MaterialIconKind.FolderZipOutline,
            AccentColor = "#38BDF8",
            Badge = "I/O",
            Description = "Streams, directory operations, and serializing data as JSON, XML, or binary.",
            Articles = new List<DocArticle>
            {
                CreateFileSerializationArticle()
            }
        };
    }

    private DocArticle CreateFileSerializationArticle()
    {
        return new DocArticle
        {
            Id = "learn_file_serialization",
            Title = "File Handling & Serialization",
            Subtitle = "Go beyond basic reads and writes: work with streams directly, enumerate directories efficiently, and serialize objects as JSON, XML, or raw binary.",
            ReadingTime = "8 min read",
            Summary = "Once you outgrow File.ReadAllText and File.WriteAllText, streams give you fine-grained control over how bytes move between memory, disk, and the network — and are the foundation every serializer builds on, whether it's producing JSON, XML, or a compact binary format.",
            Keywords = new List<string> { "stream", "filestream", "streamreader", "copytoasync", "directory.enumeratefiles", "xmlserializer", "xml serialization", "binarywriter", "binaryreader", "binary serialization", "json stream", "serializeasync", "deserializeasync" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "File & Directory I/O",
                    Content = "The Learn C# category has a dedicated File I/O article covering File.ReadAllText, File.WriteAllText, and the async equivalents — start there for the basics. This article goes a step further into how directories scale and how files move as raw byte streams rather than whole strings.",
                    BulletPoints = new List<string>
                    {
                        "Directory.GetFiles returns a fully-populated string[] — every match is read into memory before you see the first result.",
                        "Directory.EnumerateFiles returns an IEnumerable<string> that yields matches lazily as it walks the file system, which matters a lot on folders with thousands of entries.",
                        "File.Copy, File.Move, and File.Delete cover whole-file operations; pass overwrite: true to File.Copy to replace an existing destination.",
                        "FileInfo and DirectoryInfo wrap a single path and expose Length, LastWriteTimeUtc, and other metadata without extra static-method calls."
                    },
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Prefer Directory.EnumerateFiles over Directory.GetFiles whenever you're filtering or stopping early (e.g. with a LINQ .Where or .FirstOrDefault) — enumeration lets you skip work instead of paying to list every file up front."
                },
                new()
                {
                    Heading = "Streams",
                    Content = "A Stream is an abstraction over a sequence of bytes, whatever is actually backing it — a file on disk, a block of memory, or a network socket. FileStream is the disk-backed implementation, and it's what File.ReadAllText and friends use internally. Working with a Stream directly means you control the buffer size, whether reads are synchronous or asynchronous, and how much of the source ends up in memory at once.",
                    BulletPoints = new List<string>
                    {
                        "Stream implements IDisposable — always wrap one in a using statement (or await using for async disposal) so the underlying file handle is released promptly.",
                        "Stream.CopyToAsync(destination) copies the remaining contents of one stream into another using an internal buffer, without ever materializing the whole payload as a byte[] or string.",
                        "StreamReader and StreamWriter adapt a byte Stream into text, handling the character encoding (UTF-8 by default) for you.",
                        "FileStream's constructor lets you choose a FileMode (Open, Create, Append, ...) and FileAccess (Read, Write, ReadWrite) explicitly, which the File.* helpers pick for you implicitly."
                    },
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Streams compose: a FileStream can be wrapped in a GZipStream for compression, which can itself be wrapped in a StreamWriter for text — each layer only needs to know about the Stream underneath it."
                },
                new()
                {
                    Heading = "JSON Serialization (System.Text.Json)",
                    Content = "The Learn C# category's JSON article covers JsonSerializer.Serialize and Deserialize for the common case of converting an object to or from a JSON string. When the JSON is large, or you're writing straight to a file, that intermediate string is wasted work — JsonSerializer.SerializeAsync and DeserializeAsync operate directly on a Stream, so the JSON is produced or consumed incrementally instead of being built up as one big string in memory first.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "SerializeAsync/DeserializeAsync accept a CancellationToken, which makes them a natural fit for long-running exports or imports that the user might cancel partway through."
                },
                new()
                {
                    Heading = "XML Serialization",
                    Content = "System.Xml.Serialization.XmlSerializer converts a plain object graph to and from XML, the same way JsonSerializer does for JSON. It's older than System.Text.Json and more particular about its inputs: the type needs a public parameterless constructor, and only public read/write properties and fields are serialized by default. Attributes like [XmlRoot], [XmlElement], and [XmlAttribute] let you control the element and attribute names XmlSerializer produces, similar to how [JsonPropertyName] works for JSON.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "XmlSerializer.Serialize does not add an XML declaration or format collections the way you might expect without configuration — pass an XmlWriter created with XmlWriterSettings { Indent = true } if you want readable, pretty-printed output."
                },
                new()
                {
                    Heading = "Binary Serialization (Basics)",
                    Content = "BinaryWriter and BinaryReader write and read primitive types — int, double, bool, string, and so on — as compact binary data instead of text. There's no schema or field names in the output, so the reader has to know the exact order and types the writer used; get that order wrong and you'll read garbage or throw an exception. This makes binary I/O fast and small, but brittle to change, which is why it's best reserved for tightly-controlled formats like a game's save file or a custom cache, not for data you'll exchange with another system.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Avoid the legacy BinaryFormatter class for anything beyond a quick local experiment — Microsoft has marked it obsolete and unsafe for untrusted input because deserializing crafted binary data with it can execute arbitrary code. BinaryWriter/BinaryReader, which only read the primitive values you explicitly write, don't have that problem."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "File.ReadAllTextAsync", ReturnType = "Task<string>", Parameters = "string path", Description = "Asynchronously reads an entire file's contents into a string." },
                new() { MethodName = "Directory.EnumerateFiles", ReturnType = "IEnumerable<string>", Parameters = "string path, string searchPattern = \"*\"", Description = "Lazily yields the full paths of files in a directory that match a search pattern, without buffering the whole list up front." },
                new() { MethodName = "Stream.CopyToAsync", ReturnType = "Task", Parameters = "Stream destination", Description = "Asynchronously reads the bytes remaining in this stream and writes them to another stream, using an internal buffer." },
                new() { MethodName = "XmlSerializer.Serialize", ReturnType = "void", Parameters = "Stream stream, object? o", Description = "Serializes an object graph to XML and writes it to the given stream." },
                new() { MethodName = "BinaryWriter.Write", ReturnType = "void", Parameters = "<primitive> value", Description = "Writes a primitive value (int, double, string, bool, etc.) to the underlying stream in binary form. Overloaded for each supported type." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_fileser_streams",
                    Title = "FileStream, StreamReader & CopyToAsync",
                    Description = "Open a file as a raw byte stream, read it as text with StreamReader, and copy one file to another without loading it fully into memory.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;
                    using System.Text;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-file-serialization-demo");
                    Directory.CreateDirectory(dir);
                    string sourcePath = Path.Combine(dir, "source.txt");
                    string copyPath = Path.Combine(dir, "copy.txt");

                    await using (var writeStream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write))
                    await using (var writer = new StreamWriter(writeStream, Encoding.UTF8))
                    {
                        await writer.WriteLineAsync("First line written through a FileStream.");
                        await writer.WriteLineAsync("Second line, same stream.");
                    }

                    // Read it back with a StreamReader wrapping a FileStream opened explicitly.
                    using (var readStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                    using (var reader = new StreamReader(readStream, Encoding.UTF8))
                    {
                        Console.WriteLine("Contents:");
                        Console.WriteLine(await reader.ReadToEndAsync());
                    }

                    // Copy the file to a new path by copying the underlying byte streams directly.
                    await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                    await using (var destination = new FileStream(copyPath, FileMode.Create, FileAccess.Write))
                    {
                        await source.CopyToAsync(destination);
                    }

                    Console.WriteLine($"Copied {new FileInfo(copyPath).Length} bytes to {copyPath}");
                    """
                },
                new()
                {
                    Id = "snip_learn_fileser_json_stream",
                    Title = "Streaming JSON Straight to a File",
                    Description = "Use JsonSerializer.SerializeAsync/DeserializeAsync to write and read JSON directly against a FileStream, skipping the intermediate string.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;
                    using System.IO;
                    using System.Text.Json;
                    using System.Threading.Tasks;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-file-serialization-demo");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "catalog.json");

                    var items = new List<CatalogItem>
                    {
                        new("Wireless Mouse", 24.99m),
                        new("Mechanical Keyboard", 89.00m),
                        new("USB-C Hub", 34.50m)
                    };

                    await using (var writeStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        await JsonSerializer.SerializeAsync(writeStream, items, new JsonSerializerOptions { WriteIndented = true });
                    }

                    Console.WriteLine($"Wrote JSON directly to {path} without building an intermediate string.");

                    await using (var readStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        var roundTripped = await JsonSerializer.DeserializeAsync<List<CatalogItem>>(readStream);
                        foreach (var item in roundTripped!)
                        {
                            Console.WriteLine($" - {item.Name}: {item.Price:C}");
                        }
                    }

                    record CatalogItem(string Name, decimal Price);
                    """
                },
                new()
                {
                    Id = "snip_learn_fileser_xml",
                    Title = "XML Serialization Round-Trip",
                    Description = "Serialize a plain class to an XML file with XmlSerializer, then deserialize it back.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;
                    using System.Xml.Serialization;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-file-serialization-demo");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "employee.xml");

                    var employee = new Employee { Id = 42, Name = "Ada Lovelace", Department = "Engineering" };

                    var serializer = new XmlSerializer(typeof(Employee));

                    using (var writeStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        serializer.Serialize(writeStream, employee);
                    }

                    Console.WriteLine(File.ReadAllText(path));

                    using (var readStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        var restored = (Employee)serializer.Deserialize(readStream)!;
                        Console.WriteLine($"Restored: #{restored.Id} {restored.Name} ({restored.Department})");
                    }

                    // XmlSerializer requires a public parameterless constructor and public properties.
                    [XmlRoot("Employee")]
                    public class Employee
                    {
                        [XmlAttribute("id")]
                        public int Id { get; set; }

                        [XmlElement("Name")]
                        public string Name { get; set; } = string.Empty;

                        [XmlElement("Department")]
                        public string Department { get; set; } = string.Empty;
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_fileser_binary",
                    Title = "BinaryWriter & BinaryReader Round-Trip",
                    Description = "Write a handful of primitive values as compact binary data, then read them back in the exact same order.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-file-serialization-demo");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "save.bin");

                    using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(3);                 // level
                        writer.Write(1542.75);            // score
                        writer.Write(true);                // hasSavedGame
                        writer.Write("Ada");              // playerName
                    }

                    Console.WriteLine($"Wrote {new FileInfo(path).Length} bytes of raw binary data to {path}");

                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    using (var reader = new BinaryReader(stream))
                    {
                        // Values must be read back in the exact order they were written.
                        int level = reader.ReadInt32();
                        double score = reader.ReadDouble();
                        bool hasSavedGame = reader.ReadBoolean();
                        string playerName = reader.ReadString();

                        Console.WriteLine($"Level {level}, Score {score}, HasSavedGame {hasSavedGame}, Player {playerName}");
                    }
                    """
                }
            }
        };
    }
}
