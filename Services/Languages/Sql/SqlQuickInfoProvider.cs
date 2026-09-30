using System.Collections.Frozen;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Produces hover (Quick Info) documentation for SQL keywords, statements, and SQLite functions.
/// </summary>
public static class SqlQuickInfoProvider
{
    public sealed record QuickInfo(string Signature, string Summary, string? Module = null);

    private static readonly FrozenDictionary<string, QuickInfo> Entries = BuildEntries().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static QuickInfo? Lookup(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;
        return Entries.TryGetValue(symbol.Trim(), out var info) ? info : null;
    }

    public static string? ExtractSymbol(string code, int position)
    {
        if (string.IsNullOrEmpty(code) || position < 0 || position > code.Length) return null;
        if (position == code.Length) position--;

        int start = position;
        while (start > 0 && IsIdentChar(code[start - 1])) start--;

        int end = position;
        while (end < code.Length && IsIdentChar(code[end])) end++;

        if (start >= end) return null;

        var symbol = code[start..end];

        // Check for two-word keywords: GROUP BY, ORDER BY, CREATE TABLE, INSERT INTO, etc.
        if (start > 0 && code[start - 1] == ' ')
        {
            int prevStart = start - 1;
            while (prevStart > 0 && code[prevStart - 1] == ' ') prevStart--;
            int wordStart = prevStart;
            while (wordStart > 0 && IsIdentChar(code[wordStart - 1])) wordStart--;

            if (wordStart < prevStart)
            {
                var twoWord = $"{code[wordStart..prevStart]} {symbol}".Trim();
                if (Entries.ContainsKey(twoWord)) return twoWord;
            }
        }

        return symbol;
    }

    private static bool IsIdentChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '.';

    private static Dictionary<string, QuickInfo> BuildEntries()
    {
        var d = new Dictionary<string, QuickInfo>(StringComparer.OrdinalIgnoreCase);

        // SQL Keywords & Clauses
        d["SELECT"] = new("SELECT [DISTINCT] column1, column2 FROM table", "Retrieves specified rows and columns from one or more tables, views, or CTEs.", "SQL DML");
        d["FROM"] = new("FROM table_name [AS alias]", "Specifies the source tables, views, or subqueries to query data from.", "SQL DML");
        d["WHERE"] = new("WHERE condition", "Filters rows before grouping based on search criteria predicates.", "SQL DML");
        d["GROUP BY"] = new("GROUP BY column1, column2", "Groups summary rows by common values across specified columns.", "SQL DML");
        d["HAVING"] = new("HAVING aggregate_condition", "Filters grouped result sets based on aggregate conditions.", "SQL DML");
        d["ORDER BY"] = new("ORDER BY column [ASC | DESC]", "Sorts the resulting rows by specified column values.", "SQL DML");
        d["LIMIT"] = new("LIMIT count [OFFSET offset]", "Constrains the number of rows returned by the query.", "SQL DML");
        d["JOIN"] = new("[INNER | LEFT | CROSS] JOIN table ON condition", "Combines records from two tables based on a matching predicate.", "SQL DML");
        d["INSERT"] = new("INSERT INTO table (col1, col2) VALUES (val1, val2)", "Inserts new records into a database table.", "SQL DML");
        d["INSERT INTO"] = new("INSERT INTO table (col1, col2) VALUES (val1, val2)", "Inserts new records into a database table.", "SQL DML");
        d["UPDATE"] = new("UPDATE table SET col1 = val1 WHERE condition", "Modifies existing rows in a database table.", "SQL DML");
        d["DELETE"] = new("DELETE FROM table WHERE condition", "Removes rows matching a condition from a database table.", "SQL DML");
        d["CREATE TABLE"] = new("CREATE TABLE [IF NOT EXISTS] name (col type constraints, ...)", "Creates a new database table schema.", "SQL DDL");
        d["PRAGMA"] = new("PRAGMA pragma_name [= value]", "Queries or modifies internal operational parameters of the SQLite database engine.", "SQLite Core");

        // Built-in Aggregate & Scalar Functions
        d["COUNT"] = new("COUNT(expression | *) -> INTEGER", "Returns total count of matching rows or non-null values.", "SQLite Aggregate");
        d["SUM"] = new("SUM(numeric_col) -> NUMERIC", "Returns the arithmetic sum of all numeric values in a column.", "SQLite Aggregate");
        d["AVG"] = new("AVG(numeric_col) -> REAL", "Returns the arithmetic mean of all numeric values in a column.", "SQLite Aggregate");
        d["MIN"] = new("MIN(col) -> ANY", "Returns the minimum value in a column or argument list.", "SQLite Function");
        d["MAX"] = new("MAX(col) -> ANY", "Returns the maximum value in a column or argument list.", "SQLite Function");
        d["COALESCE"] = new("COALESCE(val1, val2, ...) -> ANY", "Evaluates arguments from left to right and returns the first non-null value.", "SQLite Core");
        d["IFNULL"] = new("IFNULL(val1, val2) -> ANY", "Returns val1 if not null, otherwise returns val2.", "SQLite Core");
        d["NULLIF"] = new("NULLIF(val1, val2) -> ANY", "Returns NULL if val1 equals val2, otherwise returns val1.", "SQLite Core");
        d["ROUND"] = new("ROUND(number, [digits]) -> REAL", "Rounds a floating-point number to the specified number of decimal digits.", "SQLite Math");
        d["LOWER"] = new("LOWER(text) -> TEXT", "Converts text characters to lowercase.", "SQLite String");
        d["UPPER"] = new("UPPER(text) -> TEXT", "Converts text characters to uppercase.", "SQLite String");
        d["LENGTH"] = new("LENGTH(text_or_blob) -> INTEGER", "Returns the character length of a string or byte count of a blob.", "SQLite String");
        d["PRINTF"] = new("PRINTF(format, arg1, ...) -> TEXT", "Formats text according to a C-style printf format specification.", "SQLite String");
        d["STRFTIME"] = new("STRFTIME(format, timestring, ...) -> TEXT", "Formats date/time values using standard strftime modifiers.", "SQLite Date");
        d["DATE"] = new("DATE(timestring, ...) -> TEXT", "Returns date formatted as 'YYYY-MM-DD'.", "SQLite Date");
        d["DATETIME"] = new("DATETIME(timestring, ...) -> TEXT", "Returns date and time formatted as 'YYYY-MM-DD HH:MM:SS'.", "SQLite Date");

        return d;
    }
}
