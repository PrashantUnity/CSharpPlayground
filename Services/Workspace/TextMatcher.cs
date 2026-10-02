using System.Text;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

/// <summary>Receives one match: its 1-based line and column, its length, and the text of that line (without the line break).</summary>
public delegate void TextMatchHandler(int line, int column, int length, ReadOnlySpan<char> lineText);

/// <summary>
/// What the Search panel looks for: a piece of text or a regular expression, with the panel's three options (match case,
/// whole word, regex). One matcher serves the open document and every file of the workspace. Matching is per line, works on
/// spans and allocates nothing per line, so a search over a big project is bounded by reading the files, not by this.
/// </summary>
public sealed class TextMatcher
{
    // A pattern that backtracks without end must not freeze a search of thousands of files.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    private readonly string _query;
    private readonly StringComparison _comparison;
    private readonly bool _needsWordStart;
    private readonly bool _needsWordEnd;
    private readonly Regex? _regex;

    public TextMatcher(string? query, bool matchCase, bool wholeWord, bool useRegex)
    {
        _query = query ?? string.Empty;
        _comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (useRegex && _query.Length > 0)
        {
            try
            {
                var pattern = wholeWord ? $@"\b(?:{_query})\b" : _query;
                _regex = new Regex(pattern, RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase), RegexTimeout);
            }
            catch (ArgumentException ex)
            {
                Error = ex.Message;
            }
        }
        else if (wholeWord && _query.Length > 0)
        {
            // Like \b: a boundary is only required next to a letter, digit or underscore (a query of ".Foo" may follow a word).
            _needsWordStart = IsWordChar(_query[0]);
            _needsWordEnd = IsWordChar(_query[^1]);
        }
    }

    /// <summary>Why the regular expression could not be used (null when there is none or it is fine).</summary>
    public string? Error { get; }

    /// <summary>True when there is something to look for.</summary>
    public bool IsValid => _query.Length > 0 && Error == null;

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>Finds the first match in <paramref name="line"/> at or after <paramref name="start"/>.</summary>
    public bool TryFind(ReadOnlySpan<char> line, int start, out int index, out int length)
    {
        index = 0;
        length = 0;
        if (Error != null || _query.Length == 0 || start > line.Length) return false;

        if (_regex != null)
        {
            try
            {
                foreach (var match in _regex.EnumerateMatches(line, start))
                {
                    // An empty match (a pattern like "x*" or "^") points at nothing worth listing or replacing.
                    if (match.Length == 0) continue;
                    index = match.Index;
                    length = match.Length;
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Too costly on this line: treat it as having no match.
            }

            return false;
        }

        int from = start;
        while (from <= line.Length - _query.Length)
        {
            int found = line[from..].IndexOf(_query, _comparison);
            if (found < 0) return false;
            found += from;

            if ((!_needsWordStart || found == 0 || !IsWordChar(line[found - 1])) &&
                (!_needsWordEnd || found + _query.Length == line.Length || !IsWordChar(line[found + _query.Length])))
            {
                index = found;
                length = _query.Length;
                return true;
            }

            from = found + 1;
        }

        return false;
    }

    /// <summary>
    /// Reports every match in <paramref name="text"/>, line by line, and returns how many there were (stopping at
    /// <paramref name="limit"/>). Lines end at <c>\n</c>; a <c>\r</c> before it is not part of the line.
    /// </summary>
    public int Scan(string text, TextMatchHandler onMatch, int limit = int.MaxValue, CancellationToken cancellationToken = default)
    {
        if (!IsValid || text.Length == 0 || limit <= 0) return 0;

        // A plain query that is nowhere in the text needs no line splitting at all.
        if (_regex == null && text.AsSpan().IndexOf(_query, _comparison) < 0) return 0;

        var span = text.AsSpan();
        int count = 0;
        int lineNumber = 0;
        int position = 0;
        while (true)
        {
            int lineBreak = span[position..].IndexOf('\n');
            var line = lineBreak < 0 ? span[position..] : span.Slice(position, lineBreak);
            if (line.Length > 0 && line[^1] == '\r') line = line[..^1];
            lineNumber++;

            int from = 0;
            while (TryFind(line, from, out var index, out var length))
            {
                onMatch(lineNumber, index + 1, length, line);
                if (++count >= limit) return count;
                from = index + length;
            }

            if (lineBreak < 0) return count;
            position += lineBreak + 1;
            if ((lineNumber & 0x3FF) == 0) cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// The text with its matches replaced (at most <paramref name="limit"/> of them), line breaks kept as they were. In
    /// regular-expression mode the replacement may use groups (<c>$1</c>), as in the panel of any editor.
    /// </summary>
    public string Replace(string text, string replacement, out int replaced, int limit = int.MaxValue)
    {
        replaced = 0;
        if (!IsValid || text.Length == 0 || limit <= 0) return text;
        if (_regex == null && text.AsSpan().IndexOf(_query, _comparison) < 0) return text;

        var result = new StringBuilder(text.Length);
        int position = 0;
        while (position <= text.Length)
        {
            int lineBreak = text.IndexOf('\n', position);
            int lineEnd = lineBreak < 0 ? text.Length : lineBreak;
            int contentEnd = lineEnd > position && text[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
            var line = text.AsSpan(position, contentEnd - position);

            int from = 0;
            int copied = 0;
            while (replaced < limit && TryFind(line, from, out var index, out var length))
            {
                result.Append(line[copied..index]);
                result.Append(ReplacementFor(line, index, length, replacement));
                copied = index + length;
                from = copied;
                replaced++;
            }

            result.Append(line[copied..]);
            result.Append(text, contentEnd, (lineBreak < 0 ? text.Length : lineBreak + 1) - contentEnd);
            if (lineBreak < 0) break;
            position = lineBreak + 1;
        }

        return replaced == 0 ? text : result.ToString();
    }

    private string ReplacementFor(ReadOnlySpan<char> line, int index, int length, string replacement)
    {
        if (_regex == null) return replacement;

        // Groups (and lookarounds) are worked out against the whole line, as the match was found.
        var match = _regex.Match(line.ToString(), index);
        return match.Success && match.Index == index && match.Length == length ? match.Result(replacement) : replacement;
    }
}
