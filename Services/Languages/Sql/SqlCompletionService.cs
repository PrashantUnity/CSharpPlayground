using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Provides code completion suggestions for SQL scripts and notebook cells:
/// SQLite dot-commands (after .), DDL/DML keywords, data types, built-in functions, and local table definitions.
/// </summary>
public sealed partial class SqlCompletionService : ILanguageCompletionService
{
    [GeneratedRegex(@"\bCREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>[a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateTableRegex();

    [GeneratedRegex(@"\bCREATE\s+VIEW\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>[a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateViewRegex();

    public Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
        string code,
        int caretOffset,
        EditorAssistantContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(code) || caretOffset < 0 || caretOffset > code.Length)
        {
            return Task.FromResult<IReadOnlyList<CSharpCompletionItem>>(Array.Empty<CSharpCompletionItem>());
        }

        // Check if cursor is immediately after a dot (SQLite dot command)
        int wordStart = caretOffset;
        while (wordStart > 0 && (char.IsLetterOrDigit(code[wordStart - 1]) || code[wordStart - 1] == '_'))
        {
            wordStart--;
        }

        if (wordStart > 0 && code[wordStart - 1] == '.')
        {
            var prefix = code[wordStart..caretOffset];
            return Task.FromResult(GetDotCommandCompletions(prefix));
        }

        var generalPrefix = code[wordStart..caretOffset];
        return Task.FromResult(GetGeneralCompletions(code, generalPrefix));
    }

    private static IReadOnlyList<CSharpCompletionItem> GetDotCommandCompletions(string prefix)
    {
        var dotCommands = new (string Name, string Description)[]
        {
            (".tables", "List names of tables matching LIKE pattern"),
            (".schema", "Show the CREATE statements matching pattern"),
            (".mode", "Set output mode: table, column, json, csv, line"),
            (".header", "Turn display of headers on or off"),
            (".read", "Execute SQL from a file"),
            (".databases", "List names and files of attached databases"),
            (".indices", "List names of indexes"),
            (".dump", "Render database content as SQL"),
            (".nullvalue", "Use STRING in place of NULL values"),
            (".show", "Show the current values for various settings"),
            (".exit", "Exit SQLite prompt")
        };

        var list = new List<CSharpCompletionItem>();
        foreach (var (cmd, desc) in dotCommands)
        {
            var clean = cmd.TrimStart('.');
            Add(list, prefix, cmd, cmd, desc, CompletionItemKind.Method);
        }
        return list;
    }

    private static IReadOnlyList<CSharpCompletionItem> GetGeneralCompletions(string code, string prefix)
    {
        var list = new List<CSharpCompletionItem>();

        // Keywords and snippets
        var keywords = new (string Word, string Snippet, string Desc, CompletionItemKind Kind)[]
        {
            ("SELECT", "SELECT ", "Retrieves data from one or more tables.", CompletionItemKind.Keyword),
            ("FROM", "FROM ", "Specifies the table or view to query from.", CompletionItemKind.Keyword),
            ("WHERE", "WHERE ", "Filters rows based on a search condition.", CompletionItemKind.Keyword),
            ("GROUP BY", "GROUP BY ", "Groups rows with identical values in summary columns.", CompletionItemKind.Keyword),
            ("HAVING", "HAVING ", "Filters groups created by GROUP BY.", CompletionItemKind.Keyword),
            ("ORDER BY", "ORDER BY ", "Sorts query result set by specified columns.", CompletionItemKind.Keyword),
            ("LIMIT", "LIMIT ", "Constrains the number of rows returned.", CompletionItemKind.Keyword),
            ("OFFSET", "OFFSET ", "Skips specified number of rows before returning rows.", CompletionItemKind.Keyword),
            ("JOIN", "JOIN ", "Combines rows from two or more tables.", CompletionItemKind.Keyword),
            ("LEFT JOIN", "LEFT JOIN ", "Returns all records from the left table and matched from right.", CompletionItemKind.Keyword),
            ("INNER JOIN", "INNER JOIN ", "Returns records that have matching values in both tables.", CompletionItemKind.Keyword),
            ("INSERT INTO", "INSERT INTO ", "Inserts new rows into a table.", CompletionItemKind.Keyword),
            ("VALUES", "VALUES ", "Specifies row values to insert.", CompletionItemKind.Keyword),
            ("UPDATE", "UPDATE ", "Modifies existing rows in a table.", CompletionItemKind.Keyword),
            ("SET", "SET ", "Specifies columns and values to update.", CompletionItemKind.Keyword),
            ("DELETE FROM", "DELETE FROM ", "Deletes rows from a table.", CompletionItemKind.Keyword),
            ("CREATE TABLE", "CREATE TABLE IF NOT EXISTS ", "Creates a new database table.", CompletionItemKind.Keyword),
            ("DROP TABLE", "DROP TABLE IF EXISTS ", "Removes a table definition and data.", CompletionItemKind.Keyword),
            ("ALTER TABLE", "ALTER TABLE ", "Modifies an existing table structure.", CompletionItemKind.Keyword),
            ("PRAGMA", "PRAGMA ", "Queries or modifies SQLite operational parameters.", CompletionItemKind.Keyword),
            ("UNION ALL", "UNION ALL ", "Combines results of two queries without removing duplicates.", CompletionItemKind.Keyword),
            ("DISTINCT", "DISTINCT ", "Filters duplicate rows from result set.", CompletionItemKind.Keyword),
            ("PRIMARY KEY", "PRIMARY KEY ", "Uniquely identifies each record in a table.", CompletionItemKind.Keyword),
            ("FOREIGN KEY", "FOREIGN KEY ", "Defines a relationship to another table.", CompletionItemKind.Keyword),
            ("AUTOINCREMENT", "AUTOINCREMENT", "Automatically increments integer primary key.", CompletionItemKind.Keyword)
        };

        foreach (var (word, snippet, desc, kind) in keywords)
        {
            Add(list, prefix, word, snippet, desc, kind);
        }

        // Data types
        var types = new[] { "INTEGER", "TEXT", "REAL", "BLOB", "NUMERIC", "BOOLEAN", "DATETIME", "DATE", "VARCHAR(255)" };
        foreach (var t in types)
        {
            Add(list, prefix, t, t, $"SQLite data type {t}", CompletionItemKind.Class);
        }

        // Functions
        var functions = new (string Name, string Snippet, string Desc)[]
        {
            ("COUNT", "COUNT(*)", "Returns the number of rows matching criteria."),
            ("SUM", "SUM()", "Returns total sum of a numeric column."),
            ("AVG", "AVG()", "Returns the average value of a numeric column."),
            ("MIN", "MIN()", "Returns the smallest value in a column."),
            ("MAX", "MAX()", "Returns the largest value in a column."),
            ("COALESCE", "COALESCE()", "Returns first non-null argument."),
            ("IFNULL", "IFNULL()", "Returns first non-null argument, or second argument."),
            ("NULLIF", "NULLIF()", "Returns NULL if both arguments are equal."),
            ("ROUND", "ROUND()", "Rounds numeric expression to specified decimals."),
            ("LOWER", "LOWER()", "Converts string to lowercase."),
            ("UPPER", "UPPER()", "Converts string to uppercase."),
            ("LENGTH", "LENGTH()", "Returns character or byte length of expression."),
            ("SUBSTR", "SUBSTR()", "Returns substring of a string."),
            ("PRINTF", "PRINTF()", "Formats text according to format string."),
            ("STRFTIME", "STRFTIME()", "Formats date/time string."),
            ("DATE", "DATE('now')", "Returns current date (YYYY-MM-DD)."),
            ("DATETIME", "DATETIME('now')", "Returns current date and time.")
        };

        foreach (var (name, snippet, desc) in functions)
        {
            Add(list, prefix, name, snippet, desc, CompletionItemKind.Method);
        }

        // Tables defined in code
        foreach (Match match in CreateTableRegex().Matches(code))
        {
            var tbl = match.Groups["name"].Value;
            Add(list, prefix, tbl, tbl, $"User table '{tbl}'", CompletionItemKind.Struct);
        }

        return list;
    }

    private static void Add(List<CSharpCompletionItem> list, string prefix, string text, string snippet, string doc, CompletionItemKind kind)
    {
        if (!string.IsNullOrEmpty(prefix) && !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        list.Add(new CSharpCompletionItem
        {
            DisplayText = text,
            InsertionText = snippet,
            Signature = text,
            Documentation = doc,
            Kind = kind
        });
    }
}
