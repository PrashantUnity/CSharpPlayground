using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Turns an XML documentation comment (<c>&lt;summary&gt;</c>, <c>&lt;param&gt;</c>, <c>&lt;returns&gt;</c>, ...) into
/// the runs the hover draws: XML indentation collapsed to single spaces, <c>&lt;see cref&gt;</c> reduced to the
/// symbol's short name, <c>&lt;para&gt;</c> and lists turned into line breaks.
/// </summary>
public static partial class XmlDocumentationParser
{
    private static readonly string[] XrefPrefixes = ["T:", "M:", "P:", "F:", "E:"];

    /// <summary>
    /// Parses what <c>ISymbol.GetDocumentationCommentXml()</c> returns: a whole <c>&lt;member&gt;</c> element for source
    /// symbols, or just its inner XML for metadata symbols. Returns null for empty or malformed XML.
    /// </summary>
    /// <param name="resolveCref">
    /// Names and colours a <c>cref</c> such as <c>M:System.String.Split(System.Char[])</c>; returns null when the symbol
    /// can't be found, and the cref text itself is shortened instead.
    /// </param>
    public static QuickInfoDocumentation? Parse(string? xml, Func<string, QuickInfoTextRun?>? resolveCref = null)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;

        XElement root;
        try
        {
            root = XElement.Parse("<doc>" + xml + "</doc>", LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return null;
        }

        var member = root.Elements().Count() == 1 && root.Element("member") is { } single ? single : root;
        var context = new ParseContext(resolveCref);
        var inheritDoc = member.Element("inheritdoc");

        return new QuickInfoDocumentation
        {
            Summary = context.Runs(member.Element("summary")),
            TypeParameters = context.Named(member.Elements("typeparam"), QuickInfoTextKind.TypeParameter),
            Parameters = context.Named(member.Elements("param"), QuickInfoTextKind.Variable),
            Returns = context.Runs(member.Element("returns")),
            Value = context.Runs(member.Element("value")),
            Exceptions = context.Exceptions(member.Elements("exception")),
            Remarks = context.Runs(member.Element("remarks")),
            InheritDocCref = inheritDoc == null ? null : inheritDoc.Attribute("cref")?.Value ?? string.Empty,
        };
    }

    /// <summary>Fills the sections a comment leaves empty from the documentation it inherits.</summary>
    public static QuickInfoDocumentation Merge(QuickInfoDocumentation own, QuickInfoDocumentation inherited) => new()
    {
        Summary = own.Summary.Count > 0 ? own.Summary : inherited.Summary,
        TypeParameters = own.TypeParameters.Count > 0 ? own.TypeParameters : inherited.TypeParameters,
        Parameters = own.Parameters.Count > 0 ? own.Parameters : inherited.Parameters,
        Returns = own.Returns.Count > 0 ? own.Returns : inherited.Returns,
        Value = own.Value.Count > 0 ? own.Value : inherited.Value,
        Exceptions = own.Exceptions.Count > 0 ? own.Exceptions : inherited.Exceptions,
        Remarks = own.Remarks.Count > 0 ? own.Remarks : inherited.Remarks,
    };

    /// <summary>
    /// A readable name for a documentation ID when its symbol can't be resolved:
    /// <c>M:System.String.Split(System.Char[])</c> → <c>String.Split</c>, <c>T:System.Collections.Generic.List`1</c> → <c>List&lt;T&gt;</c>.
    /// </summary>
    public static string CrefToDisplayName(string cref)
    {
        var text = cref;
        var kind = 'T';
        if (text.Length > 2 && text[1] == ':')
        {
            kind = text[0];
            text = text[2..];
        }

        var paren = text.IndexOf('(');
        if (paren >= 0) text = text[..paren];

        var segments = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return cref;

        var last = CleanSegment(segments[^1]);
        if (kind is 'T' or 'N' or '!' || segments.Length == 1) return last;

        var owner = CleanSegment(segments[^2]);
        return segments[^1] is "#ctor" or "#cctor" ? owner : owner + "." + last;
    }

    private static string CleanSegment(string segment) => GenericArityRegex().Replace(segment, match =>
    {
        var arity = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return arity == 1 ? "<T>" : "<" + string.Join(", ", Enumerable.Range(1, arity).Select(i => "T" + i)) + ">";
    });

    [GeneratedRegex("``?(\\d+)")]
    private static partial Regex GenericArityRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed class ParseContext(Func<string, QuickInfoTextRun?>? resolveCref)
    {
        public IReadOnlyList<QuickInfoTextRun> Runs(XElement? element)
        {
            if (element == null) return [];
            var raw = new List<QuickInfoTextRun>();
            Append(element, raw);
            return Normalize(raw);
        }

        public IReadOnlyList<QuickInfoNamedSection> Named(IEnumerable<XElement> elements, QuickInfoTextKind nameKind) =>
            elements
                .Select(e => (Name: e.Attribute("name")?.Value, Element: e))
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .Select(e => new QuickInfoNamedSection(new QuickInfoTextRun(e.Name!, nameKind), Runs(e.Element)))
                .ToList();

        public IReadOnlyList<QuickInfoNamedSection> Exceptions(IEnumerable<XElement> elements) =>
            elements
                .Select(e => (Cref: e.Attribute("cref")?.Value, Element: e))
                .Where(e => !string.IsNullOrWhiteSpace(e.Cref))
                .Select(e => new QuickInfoNamedSection(CrefRun(e.Cref!), Runs(e.Element)))
                .ToList();

        private QuickInfoTextRun CrefRun(string cref) =>
            resolveCref?.Invoke(cref) ?? new QuickInfoTextRun(CrefToDisplayName(cref), QuickInfoTextKind.Type);

        // <xref uid="..."/> carries no kind prefix, so try each kind the way a cref would be written.
        private QuickInfoTextRun XrefRun(string uid)
        {
            foreach (var prefix in XrefPrefixes)
            {
                if (resolveCref?.Invoke(prefix + uid) is { } run) return run;
            }
            return new QuickInfoTextRun(CrefToDisplayName((uid.Contains('(') ? "M:" : "T:") + uid), QuickInfoTextKind.Type);
        }

        private void Append(XElement element, List<QuickInfoTextRun> runs)
        {
            foreach (var node in element.Nodes())
            {
                switch (node)
                {
                    case XText text:
                        runs.Add(new QuickInfoTextRun(text.Value, QuickInfoTextKind.Text));
                        break;
                    case XElement child:
                        AppendElement(child, runs);
                        break;
                }
            }
        }

        private void AppendElement(XElement element, List<QuickInfoTextRun> runs)
        {
            switch (element.Name.LocalName.ToLowerInvariant())
            {
                case "see":
                case "seealso":
                    AppendReference(element, runs);
                    break;
                case "xref":
                    if ((element.Attribute("uid") ?? element.Attribute("href"))?.Value is { Length: > 0 } uid) runs.Add(XrefRun(uid));
                    break;
                case "paramref":
                    AppendName(element, QuickInfoTextKind.Variable, runs);
                    break;
                case "typeparamref":
                    AppendName(element, QuickInfoTextKind.TypeParameter, runs);
                    break;
                case "c":
                    runs.Add(new QuickInfoTextRun(element.Value, QuickInfoTextKind.Code));
                    break;
                case "code":
                    if (element.Value.Trim().Contains('\n'))
                    {
                        runs.Add(LineBreak(2));
                        runs.Add(new QuickInfoTextRun(Dedent(element.Value), QuickInfoTextKind.CodeBlock));
                        runs.Add(LineBreak(2));
                    }
                    else
                    {
                        runs.Add(new QuickInfoTextRun(element.Value, QuickInfoTextKind.Code));
                    }
                    break;
                case "para":
                case "p":
                    runs.Add(LineBreak(2));
                    Append(element, runs);
                    runs.Add(LineBreak(2));
                    break;
                case "br":
                    runs.Add(LineBreak(1));
                    break;
                case "list":
                case "ul":
                case "ol":
                    AppendList(element, runs);
                    break;
                case "a":
                    var label = Collapse(element.Value);
                    runs.Add(new QuickInfoTextRun(label.Length > 0 ? label : element.Attribute("href")?.Value ?? string.Empty, QuickInfoTextKind.Link));
                    break;
                case "inheritdoc":
                case "example":
                case "include":
                    break;
                default:
                    Append(element, runs);
                    break;
            }
        }

        private void AppendReference(XElement element, List<QuickInfoTextRun> runs)
        {
            var content = Collapse(element.Value);
            if (element.Attribute("langword")?.Value is { Length: > 0 } langword)
            {
                runs.Add(new QuickInfoTextRun(langword, QuickInfoTextKind.Keyword));
            }
            else if (element.Attribute("cref")?.Value is { Length: > 0 } cref)
            {
                var run = CrefRun(cref);
                runs.Add(content.Length > 0 ? run with { Text = content } : run);
            }
            else if (element.Attribute("href")?.Value is { Length: > 0 } href)
            {
                runs.Add(new QuickInfoTextRun(content.Length > 0 ? content : href, QuickInfoTextKind.Link));
            }
            else if (content.Length > 0)
            {
                runs.Add(new QuickInfoTextRun(content, QuickInfoTextKind.Text));
            }
        }

        private static void AppendName(XElement element, QuickInfoTextKind kind, List<QuickInfoTextRun> runs)
        {
            if (element.Attribute("name")?.Value is { Length: > 0 } name) runs.Add(new QuickInfoTextRun(name, kind));
        }

        private void AppendList(XElement list, List<QuickInfoTextRun> runs)
        {
            var numbered = list.Name.LocalName == "ol" ||
                           string.Equals(list.Attribute("type")?.Value, "number", StringComparison.OrdinalIgnoreCase);
            var index = 1;
            foreach (var item in list.Elements())
            {
                if (item.Name.LocalName is not ("item" or "li")) continue;

                runs.Add(LineBreak(1));
                runs.Add(new QuickInfoTextRun(numbered ? $"{index++}. " : "• ", QuickInfoTextKind.Text));

                var term = item.Element("term");
                var description = item.Element("description");
                if (term == null && description == null)
                {
                    Append(item, runs);
                    continue;
                }

                if (term != null) Append(term, runs);
                if (term != null && description != null) runs.Add(new QuickInfoTextRun(" – ", QuickInfoTextKind.Text));
                if (description != null) Append(description, runs);
            }
            runs.Add(LineBreak(1));
        }

        private static QuickInfoTextRun LineBreak(int count) => new(count > 1 ? "\n\n" : "\n", QuickInfoTextKind.LineBreak);

        private static string Collapse(string text) => WhitespaceRegex().Replace(text, " ").Trim();

        private static string Dedent(string code)
        {
            var lines = code.Replace("\r\n", "\n").Split('\n').ToList();
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);

            var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
            return string.Join("\n", lines.Select(l => (l.Length >= indent ? l[indent..] : l).TrimEnd()));
        }

        /// <summary>
        /// Collapses XML indentation into single spaces, trims around line breaks and merges neighbouring text, so the
        /// view can draw the runs as they are.
        /// </summary>
        private static List<QuickInfoTextRun> Normalize(List<QuickInfoTextRun> raw)
        {
            var result = new List<QuickInfoTextRun>();
            foreach (var run in raw)
            {
                if (run.Kind == QuickInfoTextKind.LineBreak)
                {
                    TrimTrailingSpace(result);
                    if (result.Count == 0) continue;
                    if (result[^1].Kind == QuickInfoTextKind.LineBreak)
                    {
                        // Two breaks in a row (a </para> then a <para>) are one paragraph break, not two.
                        if (run.Text.Length > result[^1].Text.Length) result[^1] = run;
                        continue;
                    }
                    result.Add(run);
                    continue;
                }

                var isBlock = run.Kind == QuickInfoTextKind.CodeBlock;
                var text = isBlock ? run.Text : WhitespaceRegex().Replace(run.Text, " ");
                var atLineStart = result.Count == 0 || result[^1].Kind == QuickInfoTextKind.LineBreak;
                var afterSpace = !atLineStart && result[^1].Text.EndsWith(' ');
                if (!isBlock && (atLineStart || afterSpace)) text = text.TrimStart();
                if (text.Length == 0) continue;

                if (run.Kind == QuickInfoTextKind.Text && result.Count > 0 && result[^1].Kind == QuickInfoTextKind.Text)
                {
                    result[^1] = result[^1] with { Text = result[^1].Text + text };
                }
                else
                {
                    result.Add(run with { Text = text });
                }
            }

            TrimTrailingSpace(result);
            while (result.Count > 0 && result[^1].Kind == QuickInfoTextKind.LineBreak)
            {
                result.RemoveAt(result.Count - 1);
                TrimTrailingSpace(result);
            }
            return result;
        }

        private static void TrimTrailingSpace(List<QuickInfoTextRun> runs)
        {
            while (runs.Count > 0 && runs[^1].Kind is not (QuickInfoTextKind.LineBreak or QuickInfoTextKind.CodeBlock))
            {
                var trimmed = runs[^1].Text.TrimEnd();
                if (trimmed.Length > 0)
                {
                    runs[^1] = runs[^1] with { Text = trimmed };
                    return;
                }
                runs.RemoveAt(runs.Count - 1);
            }
        }
    }
}
