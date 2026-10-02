using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocArticle CreateJsonArticle()
    {
        return new DocArticle
        {
            Id = "learn_json_serialization",
            Title = "JSON Serialization with System.Text.Json",
            Subtitle = "Convert objects to and from JSON — the built-in, high-performance way.",
            ReadingTime = "6 min read",
            Summary = "System.Text.Json ships in the .NET runtime, needs no extra package, and is the default choice for reading and writing JSON in modern C#.",
            Keywords = new List<string> { "json", "serialize", "deserialize", "system.text.json", "jsonserializer", "jsonserializeroptions", "jsonpropertyname", "jsonignore", "datetime", "camelcase", "enum" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Serializing & Deserializing Objects",
                    Content = "JsonSerializer.Serialize turns an object into a JSON string; JsonSerializer.Deserialize<T> turns a JSON string back into an object. Records and classes both work as long as their properties or constructor parameters line up with the JSON."
                },
                new()
                {
                    Heading = "JsonSerializerOptions & Collections",
                    Content = "JsonSerializerOptions controls formatting, naming, and null handling. Lists, arrays, and dictionaries all serialize naturally; enums serialize as numbers unless you opt into JsonStringEnumConverter.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Construct one JsonSerializerOptions instance and reuse it for every call — creating a new one per call is a measurable, avoidable cost."
                },
                new()
                {
                    Heading = "DateTime, Nullable Types & Custom Property Names",
                    Content = "DateTime round-trips as an ISO-8601 string by default. Use [JsonPropertyName] to map a C# property to a different JSON key (e.g. snake_case from a REST API), and [JsonIgnore] to exclude a property entirely.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Both attributes live in System.Text.Json.Serialization — a separate using directive from System.Text.Json."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "Serialize", ReturnType = "string", Parameters = "TValue value, JsonSerializerOptions? options = null", Description = "Converts an object graph into a JSON string." },
                new() { MethodName = "Deserialize", ReturnType = "TValue?", Parameters = "string json, JsonSerializerOptions? options = null", Description = "Parses a JSON string into an instance of the given type." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_json_serialize",
                    Title = "Serialize an Object to JSON",
                    Description = "Convert a record to a compact and a pretty-printed JSON string.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Text.Json;

                    var product = new Product("Wireless Mouse", 24.99m, true);

                    string compact = JsonSerializer.Serialize(product);
                    string pretty = JsonSerializer.Serialize(product, new JsonSerializerOptions { WriteIndented = true });

                    Console.WriteLine("Compact:");
                    Console.WriteLine(compact);
                    Console.WriteLine();
                    Console.WriteLine("Indented:");
                    Console.WriteLine(pretty);

                    record Product(string Name, decimal Price, bool InStock);
                    """
                },
                new()
                {
                    Id = "snip_learn_json_deserialize",
                    Title = "Deserialize JSON to an Object",
                    Description = "Parse a JSON string back into a strongly-typed record.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Text.Json;

                    // In real code this JSON would come from a file, database, or API response.
                    string json = JsonSerializer.Serialize(new Product("Wireless Mouse", 24.99m, true));
                    Console.WriteLine($"Source JSON: {json}");

                    var product = JsonSerializer.Deserialize<Product>(json);

                    product.Dump("Deserialized Product");

                    record Product(string Name, decimal Price, bool InStock);
                    """
                },
                new()
                {
                    Id = "snip_learn_json_options",
                    Title = "Configuring JsonSerializerOptions",
                    Description = "Use camelCase property names and skip nulls when writing JSON.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Text.Json;
                    using System.Text.Json.Serialization;

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var user = new UserProfile("Ada Lovelace", 28, null);

                    string json = JsonSerializer.Serialize(user, options);
                    Console.WriteLine(json);

                    record UserProfile(string FullName, int Age, string? Nickname);
                    """
                },
                new()
                {
                    Id = "snip_learn_json_collections",
                    Title = "Serializing Collections & Dictionaries",
                    Description = "Lists and dictionaries serialize just like any other object graph.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;
                    using System.Text.Json;

                    var orders = new List<Order>
                    {
                        new("Keyboard", 2),
                        new("Monitor", 1),
                        new("USB Cable", 5)
                    };

                    var inventory = new Dictionary<string, int>
                    {
                        ["Keyboard"] = 40,
                        ["Monitor"] = 12,
                        ["USB Cable"] = 200
                    };

                    var options = new JsonSerializerOptions { WriteIndented = true };

                    Console.WriteLine(JsonSerializer.Serialize(orders, options));
                    Console.WriteLine(JsonSerializer.Serialize(inventory, options));

                    record Order(string Product, int Quantity);
                    """
                },
                new()
                {
                    Id = "snip_learn_json_enum_datetime",
                    Title = "Enums as Strings & DateTime Round-Tripping",
                    Description = "Serialize an enum as its name instead of a number, and round-trip a DateTime.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Text.Json;
                    using System.Text.Json.Serialization;

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    var shipment = new Shipment("1Z999AA10123456784", OrderStatus.Shipped, DateTime.UtcNow);

                    string json = JsonSerializer.Serialize(shipment, options);
                    Console.WriteLine(json);

                    var roundTripped = JsonSerializer.Deserialize<Shipment>(json, options);
                    Console.WriteLine($"Round-tripped status: {roundTripped.Status}, shipped at {roundTripped.ShippedAtUtc:O}");

                    enum OrderStatus { Pending, Shipped, Delivered }

                    record Shipment(string TrackingNumber, OrderStatus Status, DateTime ShippedAtUtc);
                    """
                },
                new()
                {
                    Id = "snip_learn_json_custom_names",
                    Title = "Custom Property Names with JsonPropertyName",
                    Description = "Map C# property names to different JSON keys, and hide one from serialization entirely.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Text.Json;
                    using System.Text.Json.Serialization;

                    var user = new ApiUser(101, "Grace Hopper", "never serialized");

                    string json = JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(json);

                    record ApiUser(
                        [property: JsonPropertyName("user_id")] int Id,
                        [property: JsonPropertyName("full_name")] string Name,
                        [property: JsonIgnore] string? InternalNotes);
                    """
                }
            }
        };
    }
}
