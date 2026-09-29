using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// SQL syntax highlighting definition in Dark+ and Light+ themes: SQL DDL/DML keywords, data types,
/// built-in aggregate and scalar functions, SQLite pragmas, comments, string literals, and numbers.
/// </summary>
public static class SqlSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string String, string Control,
        string Storage, string Function, string Type, string Number);

    private static readonly Palette Dark = new(
        "SQL Dark",
        Comment: "#6A9955",
        String: "#CE9178",
        Control: "#C586C0",
        Storage: "#569CD6",
        Function: "#DCDCAA",
        Type: "#4EC9B0",
        Number: "#B5CEA8");

    private static readonly Palette Light = new(
        "SQL Light",
        Comment: "#008000",
        String: "#A31515",
        Control: "#AF00DB",
        Storage: "#0000FF",
        Function: "#795E26",
        Type: "#267F99",
        Number: "#098658");

    private static readonly Lazy<IHighlightingDefinition> DarkDefinition = new(() => Load(Dark));
    private static readonly Lazy<IHighlightingDefinition> LightDefinition = new(() => Load(Light));

    public static IHighlightingDefinition Get(bool isDark) => isDark ? DarkDefinition.Value : LightDefinition.Value;

    private static IHighlightingDefinition Load(Palette palette)
    {
        using var reader = new XmlTextReader(new StringReader(Xshd(palette)));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string Xshd(Palette p) => $$$""""
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".sql" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
            <Color name="Comment" foreground="{{{p.Comment}}}" />
            <Color name="String" foreground="{{{p.String}}}" />
            <Color name="Control" foreground="{{{p.Control}}}" />
            <Color name="Storage" foreground="{{{p.Storage}}}" />
            <Color name="Function" foreground="{{{p.Function}}}" />
            <Color name="Type" foreground="{{{p.Type}}}" />
            <Color name="Number" foreground="{{{p.Number}}}" />

            <RuleSet ignoreCase="true">
                <Span color="Comment" begin="--" />
                <Span color="Comment" multiline="true" begin="/\*" end="\*/" />

                <Span color="String">
                    <Begin>'</Begin>
                    <End>'</End>
                    <RuleSet>
                        <Span begin="''" end="" />
                    </RuleSet>
                </Span>

                <Span color="String">
                    <Begin>"</Begin>
                    <End>"</End>
                </Span>

                <Span color="Storage">
                    <Begin>`</Begin>
                    <End>`</End>
                </Span>

                <Span color="Storage">
                    <Begin>\[</Begin>
                    <End>\]</End>
                </Span>

                <!-- SQLite dot commands (.read, .schema, etc.) -->
                <Rule color="Function">^\s*\.[a-zA-Z_]+</Rule>

                <!-- SQL DDL, DML and Control Flow Keywords -->
                <Keywords color="Storage">
                    <Word>SELECT</Word>
                    <Word>FROM</Word>
                    <Word>WHERE</Word>
                    <Word>INSERT</Word>
                    <Word>INTO</Word>
                    <Word>VALUES</Word>
                    <Word>UPDATE</Word>
                    <Word>SET</Word>
                    <Word>DELETE</Word>
                    <Word>CREATE</Word>
                    <Word>TABLE</Word>
                    <Word>DROP</Word>
                    <Word>ALTER</Word>
                    <Word>VIEW</Word>
                    <Word>INDEX</Word>
                    <Word>TRIGGER</Word>
                    <Word>PRAGMA</Word>
                    <Word>EXPLAIN</Word>
                    <Word>QUERY</Word>
                    <Word>PLAN</Word>
                    <Word>ATTACH</Word>
                    <Word>DETACH</Word>
                    <Word>DATABASE</Word>
                    <Word>TRANSACTION</Word>
                    <Word>BEGIN</Word>
                    <Word>COMMIT</Word>
                    <Word>ROLLBACK</Word>
                    <Word>SAVEPOINT</Word>
                    <Word>RELEASE</Word>
                    <Word>WITH</Word>
                    <Word>RECURSIVE</Word>
                    <Word>AS</Word>
                </Keywords>

                <Keywords color="Control">
                    <Word>JOIN</Word>
                    <Word>INNER</Word>
                    <Word>LEFT</Word>
                    <Word>RIGHT</Word>
                    <Word>FULL</Word>
                    <Word>OUTER</Word>
                    <Word>CROSS</Word>
                    <Word>NATURAL</Word>
                    <Word>ON</Word>
                    <Word>USING</Word>
                    <Word>GROUP</Word>
                    <Word>BY</Word>
                    <Word>HAVING</Word>
                    <Word>ORDER</Word>
                    <Word>ASC</Word>
                    <Word>DESC</Word>
                    <Word>NULLS</Word>
                    <Word>FIRST</Word>
                    <Word>LAST</Word>
                    <Word>LIMIT</Word>
                    <Word>OFFSET</Word>
                    <Word>UNION</Word>
                    <Word>ALL</Word>
                    <Word>EXCEPT</Word>
                    <Word>INTERSECT</Word>
                    <Word>DISTINCT</Word>
                    <Word>CASE</Word>
                    <Word>WHEN</Word>
                    <Word>THEN</Word>
                    <Word>ELSE</Word>
                    <Word>END</Word>
                    <Word>AND</Word>
                    <Word>OR</Word>
                    <Word>NOT</Word>
                    <Word>IN</Word>
                    <Word>LIKE</Word>
                    <Word>GLOB</Word>
                    <Word>IS</Word>
                    <Word>NULL</Word>
                    <Word>BETWEEN</Word>
                    <Word>EXISTS</Word>
                    <Word>CHECK</Word>
                    <Word>UNIQUE</Word>
                    <Word>CONSTRAINT</Word>
                    <Word>PRIMARY</Word>
                    <Word>KEY</Word>
                    <Word>FOREIGN</Word>
                    <Word>REFERENCES</Word>
                    <Word>DEFAULT</Word>
                    <Word>COLLATE</Word>
                    <Word>CASCADE</Word>
                    <Word>RESTRICT</Word>
                    <Word>ACTION</Word>
                    <Word>AUTOINCREMENT</Word>
                    <Word>TRUE</Word>
                    <Word>FALSE</Word>
                </Keywords>

                <!-- SQL Data Types -->
                <Keywords color="Type">
                    <Word>INTEGER</Word>
                    <Word>INT</Word>
                    <Word>BIGINT</Word>
                    <Word>SMALLINT</Word>
                    <Word>TINYINT</Word>
                    <Word>TEXT</Word>
                    <Word>VARCHAR</Word>
                    <Word>CHAR</Word>
                    <Word>CLOB</Word>
                    <Word>REAL</Word>
                    <Word>DOUBLE</Word>
                    <Word>FLOAT</Word>
                    <Word>NUMERIC</Word>
                    <Word>DECIMAL</Word>
                    <Word>BOOLEAN</Word>
                    <Word>DATE</Word>
                    <Word>DATETIME</Word>
                    <Word>TIMESTAMP</Word>
                    <Word>TIME</Word>
                    <Word>BLOB</Word>
                    <Word>JSON</Word>
                </Keywords>

                <!-- SQL Functions -->
                <Keywords color="Function">
                    <Word>COUNT</Word>
                    <Word>SUM</Word>
                    <Word>AVG</Word>
                    <Word>MIN</Word>
                    <Word>MAX</Word>
                    <Word>TOTAL</Word>
                    <Word>ABS</Word>
                    <Word>ROUND</Word>
                    <Word>LOWER</Word>
                    <Word>UPPER</Word>
                    <Word>LENGTH</Word>
                    <Word>SUBSTR</Word>
                    <Word>SUBSTRING</Word>
                    <Word>TRIM</Word>
                    <Word>LTRIM</Word>
                    <Word>RTRIM</Word>
                    <Word>REPLACE</Word>
                    <Word>COALESCE</Word>
                    <Word>NULLIF</Word>
                    <Word>IFNULL</Word>
                    <Word>PRINTF</Word>
                    <Word>FORMAT</Word>
                    <Word>RANDOM</Word>
                    <Word>HEX</Word>
                    <Word>ZEROBLOB</Word>
                    <Word>JULIANDAY</Word>
                    <Word>STRFTIME</Word>
                    <Word>TYPEOF</Word>
                    <Word>LAST_INSERT_ROWID</Word>
                    <Word>CHANGES</Word>
                    <Word>TOTAL_CHANGES</Word>
                    <Word>JSON_EXTRACT</Word>
                    <Word>JSON_ARRAY</Word>
                    <Word>JSON_OBJECT</Word>
                    <Word>JSON_GROUP_ARRAY</Word>
                    <Word>JSON_GROUP_OBJECT</Word>
                    <Word>ROW_NUMBER</Word>
                    <Word>RANK</Word>
                    <Word>DENSE_RANK</Word>
                    <Word>OVER</Word>
                    <Word>PARTITION</Word>
                </Keywords>

                <Rule color="Number">\b0x[0-9a-fA-F]+\b|\b\d+(\.[0-9]+)?\b</Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
