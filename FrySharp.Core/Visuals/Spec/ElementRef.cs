using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

/// <summary>What an <see cref="ElementRef"/> points at.</summary>
public enum ElementRefKind
{
    Item,
    Node,
    Cell,
    Edge
}

/// <summary>
/// One element of a visual: an item by index (<c>3</c>), a node by id (<c>"n1"</c>), a grid cell (<c>[2, 3]</c>) or an
/// edge (<c>{"from": "a", "to": "b"}</c>). Highlights, pointers and events all name elements this way.
/// </summary>
[JsonConverter(typeof(ElementRefJsonConverter))]
public readonly struct ElementRef : IEquatable<ElementRef>
{
    private ElementRef(ElementRefKind kind, int index = 0, int row = 0, int col = 0, string? id = null, string? to = null)
    {
        Kind = kind;
        Index = index;
        Row = row;
        Col = col;
        Id = id;
        To = to;
    }

    public ElementRefKind Kind { get; }

    /// <summary>The item's index (<see cref="ElementRefKind.Item"/>).</summary>
    public int Index { get; }

    public int Row { get; }
    public int Col { get; }

    /// <summary>The node's id, or the edge's first node.</summary>
    public string? Id { get; }

    /// <summary>The edge's second node.</summary>
    public string? To { get; }

    public static ElementRef Item(int index) => new(ElementRefKind.Item, index: index);
    public static ElementRef Node(string id) => new(ElementRefKind.Node, id: id ?? throw new ArgumentNullException(nameof(id)));
    public static ElementRef Cell(int row, int col) => new(ElementRefKind.Cell, row: row, col: col);

    public static ElementRef Edge(string from, string to) => new(ElementRefKind.Edge,
        id: from ?? throw new ArgumentNullException(nameof(from)), to: to ?? throw new ArgumentNullException(nameof(to)));

    public static implicit operator ElementRef(int index) => Item(index);
    public static implicit operator ElementRef(string id) => Node(id);
    public static implicit operator ElementRef((int Row, int Col) cell) => Cell(cell.Row, cell.Col);

    public override string ToString() => Kind switch
    {
        ElementRefKind.Item => Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ElementRefKind.Node => Id!,
        ElementRefKind.Cell => $"[{Row}, {Col}]",
        _ => $"{Id} → {To}"
    };

    public bool Equals(ElementRef other) =>
        Kind == other.Kind && Index == other.Index && Row == other.Row && Col == other.Col && Id == other.Id && To == other.To;

    public override bool Equals(object? obj) => obj is ElementRef other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Kind, Index, Row, Col, Id, To);
    public static bool operator ==(ElementRef left, ElementRef right) => left.Equals(right);
    public static bool operator !=(ElementRef left, ElementRef right) => !left.Equals(right);
}

/// <summary>
/// A named marker on an element, like <c>i</c> and <c>j</c> over an array or <c>slow</c> on a list node. An array
/// pointer may sit one place before or after the items (<c>-1</c>, or <c>hi = n</c>), as loops leave them.
/// </summary>
public sealed class PointerSpec
{
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>What the pointer points at; null when the variable is null (written as null, never left out).</summary>
    [JsonRequired]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public ElementRef? At { get; set; }

    /// <summary>Optional; the studio gives each pointer its own colour otherwise.</summary>
    public string? Color { get; set; }
}

/// <summary>Reads and writes an <see cref="ElementRef"/> in its short JSON forms.</summary>
public sealed class ElementRefJsonConverter : JsonConverter<ElementRef>
{
    private const string Expected = "expected an item index (3), a node id (\"n1\"), a cell ([2, 3]) or an edge ({\"from\": \"a\", \"to\": \"b\"})";

    public override ElementRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number when reader.TryGetInt32(out var index):
                return ElementRef.Item(index);
            case JsonTokenType.String:
                return ElementRef.Node(reader.GetString()!);
            case JsonTokenType.StartArray:
                return ReadCell(ref reader);
            case JsonTokenType.StartObject:
                return ReadEdge(ref reader);
            default:
                throw new JsonException(Expected);
        }
    }

    private static ElementRef ReadCell(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var row)) throw new JsonException(Expected);
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var col)) throw new JsonException(Expected);
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray) throw new JsonException(Expected);
        return ElementRef.Cell(row, col);
    }

    private static ElementRef ReadEdge(ref Utf8JsonReader reader)
    {
        string? from = null, to = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            if (!reader.Read() || reader.TokenType != JsonTokenType.String) throw new JsonException(Expected);
            switch (name)
            {
                case "from": from = reader.GetString(); break;
                case "to": to = reader.GetString(); break;
                default: throw new JsonException($"an edge has only \"from\" and \"to\", not \"{name}\"");
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject || from == null || to == null) throw new JsonException(Expected);
        return ElementRef.Edge(from, to);
    }

    public override void Write(Utf8JsonWriter writer, ElementRef value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case ElementRefKind.Item:
                writer.WriteNumberValue(value.Index);
                break;
            case ElementRefKind.Node:
                writer.WriteStringValue(value.Id);
                break;
            case ElementRefKind.Cell:
                writer.WriteStartArray();
                writer.WriteNumberValue(value.Row);
                writer.WriteNumberValue(value.Col);
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStartObject();
                writer.WriteString("from", value.Id);
                writer.WriteString("to", value.To);
                writer.WriteEndObject();
                break;
        }
    }
}
