using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocArticle CreateFileIoArticle()
    {
        return new DocArticle
        {
            Id = "learn_file_io",
            Title = "File I/O: Reading, Writing & Directories",
            Subtitle = "Read and write files synchronously and asynchronously, and work safely with paths and directories.",
            ReadingTime = "6 min read",
            Summary = "The File and Directory helper classes cover almost everything you need for everyday file work, with both synchronous and async members and a StreamReader/StreamWriter escape hatch for finer control.",
            Keywords = new List<string> { "file", "file io", "streamreader", "streamwriter", "read file", "write file", "append", "async file io", "readalltextasync", "writealltextasync", "directory", "path.combine", "file.exists" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Reading & Writing Text Files",
                    Content = "File.WriteAllText and File.ReadAllText cover the simplest case in one call each. File.AppendAllText adds to the end of an existing file (or creates it if missing) instead of overwriting it."
                },
                new()
                {
                    Heading = "Async File I/O & Streams",
                    Content = "File.WriteAllTextAsync and File.ReadAllTextAsync are async equivalents of the synchronous helpers — use them so disk I/O doesn't block a thread. For line-by-line control over a file, or a custom encoding, drop down to StreamReader and StreamWriter directly."
                },
                new()
                {
                    Heading = "Directories, Existence Checks & Safe Paths",
                    Content = "Directory.CreateDirectory makes a folder (and any missing parent folders) in one call and is safe to call even if the folder already exists. Check File.Exists / Directory.Exists before you assume something is there.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Always build paths with Path.Combine (or Path.Join) instead of concatenating strings with \"/\" or \"\\\" — it handles the platform-specific separator for you, so the same code works on Windows, macOS, and Linux."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "File.ReadAllTextAsync", ReturnType = "Task<string>", Parameters = "string path", Description = "Asynchronously reads an entire file's contents into a string." },
                new() { MethodName = "File.WriteAllTextAsync", ReturnType = "Task", Parameters = "string path, string contents", Description = "Asynchronously creates or overwrites a file with the given text." },
                new() { MethodName = "Directory.CreateDirectory", ReturnType = "DirectoryInfo", Parameters = "string path", Description = "Creates all directories in the given path that don't already exist." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_fileio_write_read",
                    Title = "Write and Read a Text File",
                    Description = "The simplest way to write and then read back a file's full contents.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-learn-csharp-demo");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "notes.txt");

                    File.WriteAllText(path, "First line written by File.WriteAllText.");

                    string contents = File.ReadAllText(path);
                    Console.WriteLine($"Read back from {path}:");
                    Console.WriteLine(contents);
                    """
                },
                new()
                {
                    Id = "snip_learn_fileio_append",
                    Title = "Append Text to a File",
                    Description = "Add new lines to an existing file without overwriting what's already there.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-learn-csharp-demo");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "log.txt");

                    File.WriteAllText(path, $"[{DateTime.Now:HH:mm:ss}] Log started.{Environment.NewLine}");
                    File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] First entry appended.{Environment.NewLine}");
                    File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] Second entry appended.{Environment.NewLine}");

                    Console.WriteLine(File.ReadAllText(path));
                    """
                },
                new()
                {
                    Id = "snip_learn_fileio_read_lines",
                    Title = "Read a File Line by Line",
                    Description = "File.ReadLines streams a file lazily instead of loading it all into memory at once.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-learn-csharp-demo");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "report.csv");

                    File.WriteAllLines(path, new[]
                    {
                        "Name,Score",
                        "Ada,98",
                        "Grace,95",
                        "Alan,91"
                    });

                    int lineNumber = 0;
                    foreach (string line in File.ReadLines(path))
                    {
                        lineNumber++;
                        Console.WriteLine($"{lineNumber}: {line}");
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_fileio_async",
                    Title = "Async Read/Write with File.*Async",
                    Description = "Read and write a file without blocking a thread while the disk I/O completes.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;
                    using System.Threading.Tasks;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-learn-csharp-demo");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "async-notes.txt");

                    await File.WriteAllTextAsync(path, "Written without blocking a thread while the disk I/O completes.");

                    string contents = await File.ReadAllTextAsync(path);
                    Console.WriteLine(contents);
                    """
                },
                new()
                {
                    Id = "snip_learn_fileio_streams",
                    Title = "StreamReader & StreamWriter",
                    Description = "Drop down to streams directly for more control, such as writing line by line.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;
                    using System.Text;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-learn-csharp-demo");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "stream-demo.txt");

                    await using (var writer = new StreamWriter(path, append: false, Encoding.UTF8))
                    {
                        await writer.WriteLineAsync("Line written through StreamWriter.");
                        await writer.WriteLineAsync("StreamWriter buffers writes for you automatically.");
                    }

                    using (var reader = new StreamReader(path, Encoding.UTF8))
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            Console.WriteLine($"> {line}");
                        }
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_fileio_directories",
                    Title = "Creating Directories & Listing Files",
                    Description = "Create a folder if it's missing, check for a file's existence, and enumerate what's there.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;
                    using System.Linq;

                    string dir = Path.Combine(Path.GetTempPath(), "frypdf-learn-csharp-demo", "reports");

                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                        Console.WriteLine($"Created directory: {dir}");
                    }

                    string filePath = Path.Combine(dir, "summary.txt");
                    if (!File.Exists(filePath))
                    {
                        File.WriteAllText(filePath, "Auto-generated summary.");
                    }

                    Console.WriteLine("Files in directory:");
                    foreach (string file in Directory.EnumerateFiles(dir).OrderBy(f => f))
                    {
                        Console.WriteLine($" - {Path.GetFileName(file)}");
                    }
                    """
                }
            }
        };
    }
}
