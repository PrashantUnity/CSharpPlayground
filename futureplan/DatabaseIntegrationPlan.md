# Universal Database Integration Plan

> **Objective**: Make FrySharp a first-class database workbench for .NET developers. Connect to relational databases (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB, DuckDB) and later to document and key-value stores (MongoDB, Redis). Browse schemas lazily even when there are tens of thousands of tables. Run queries from `.sql` files and notebook cells (`#!sql --connection prod-pg`), edit rows with a reviewed SQL diff, and generate C# from the schema. Everything stays inside the 5-zone layout.

*Last checked against the code: 2026-10-09 (branch `UiIssues`).*

---

## 0. Why, and what is different from the first draft

- **The gap is real.** Azure Data Studio was retired in early 2026, and Polyglot Notebooks, the SQL-notebook replacement Microsoft pointed people to, was deprecated at the same time ([DevClass](https://www.devclass.com/databases/2026/02/14/microsoft-deprecates-polyglot-notebooks-developers-react/4091167)). C# + SQL in one notebook is now an empty niche.
- **This revision** grounds the plan in what already exists (§1). The first draft did not mention the current SQL language at all. This revision also cuts the first milestone to a measurable vertical slice, keeps big schemas fast, moves safety enforcement into the database instead of regexes, replaces `Ctrl+S` (already Save) for committing grid edits, and fixes the notebook example so it compiles against the real `#!share` behaviour.

---

## 1. Starting point (verified in the code)

| What exists | Where | What it means for this plan |
|---|---|---|
| **SQL language module**: `.sql` files, notebook cells, aliases `sql` / `sqlite` / `sqlite3`, completion, quick info, folding, indentation | `Services/Languages/Sql/*` (15 files, ~2,000 lines) | Extend it. Don't build a parallel "database" feature next to it. |
| Execution runs through the **`sqlite3` CLI**: `SqlScriptRunner` calls `sqlite3 -header -table <db> ".read file.sql"`, and `SqlToolchainProvider` searches PATH and `SQLITE3_PATH` for it | `SqlScriptRunner.cs`, `SqlToolchainProvider*.cs` | SQL **does not work unless sqlite3 is installed**. Output is text tables, not typed results. There is no server database support. |
| `-- :database <path>` (or `:db`) directive picks the database file; `:memory:` works | `SqlScriptRunner.cs:13` | Keep it. Add `-- :connection <name>` beside it. |
| Notebook SQL uses a **throw-away temp database** per notebook (`%TEMP%/FryStudio/sql_kernel/<hash>/notebook.db`) | `SqlNotebookKernel.cs` | `#!sql --connection` is new work. The temp database stays the default. |
| `#!share --from <language> <name> [--as <new name>]` works across kernels. A SQL table shared into C# becomes a **`List<object>` of `Dictionary<string, object>` rows** | `NotebookCellDirectives.cs`, `KernelValueSharing.cs` (`Infer`/`Array`) | No `.Rows`, no types. A typed share (§8) is a real improvement. |
| Language directives take **no options** today (`#!sql` only picks the language) | `NotebookCellDirectives.cs` | `#!sql --connection x --as y` needs the directive parser extended. |
| Completion knows keywords, functions, dot-commands, and tables from `CREATE TABLE` / `CREATE VIEW` **in the same document** (regex) | `SqlCompletionService.cs` | Schema-aware completion from a live connection is new work. It needs the schema cache (§3.4). |
| Results table: `DumpTableView` uses a `VirtualizingStackPanel` for rows. `DumpTableBuilder` previews 1,000 rows. No `DataGrid` package is referenced | `Views/DumpTableView*.cs`, `Services/Display/DumpTableBuilder.cs` | Start from `DumpTableView` and add paging and editing. Adding Avalonia's DataGrid is a separate decision (§11). |
| **No secret store.** The AI API key is stored in plain text in `studio_settings.json` (`Models/AI/AiSettings.cs:83`) | `Services/Settings/StudioSettingsStore.cs` | Build one `ISecretStore` (§6) and move the AI key into it too. That fixes an existing weakness. |
| Extension points for Activity Bar, Side Bar view, Bottom Deck tab and Status Bar widget | `Services/Extensibility/Sdk/UI/UiContributionModels.cs` | The database UI can register through these instead of being hard-wired into the host ViewModel. This matches the module-kernel direction. |
| NuGet resolution and isolated load contexts | `Services/Roslyn/NuGetReferenceResolver.cs`, `Services/Extensibility/Host/ExtensionLoadContext.cs` | Reuse them for on-demand drivers (§9). No new resolver needed. |
| Lazy, virtualized tree pattern | `LazyExplorerTree`, `ExplorerRowList` (Explorer) | Reuse it for the schema tree. |
| Keybindings: `Ctrl+S` = Save in Code Studio, Notebook and Server studios. `Ctrl+Shift+Enter` is used in the notebook. `Ctrl+Enter` is free in the Code Studio editor | `Views/*StudioView.axaml.cs` | Grid commit must **not** use `Ctrl+S` (§4). |

---

## 2. Principles

1. **One SQL language, many engines.** `SqlLanguage` stays the single language. A connection decides which engine runs the text. The sqlite3 CLI becomes an optional fallback, not a requirement.
2. **Big schemas are normal.** Production databases with 50,000 tables and a million columns must open instantly. Everything is lazy, paged, cancellable and off the UI thread (`.agents/rules/performance_and_zero_lag_mandate.md`). Results stream; nothing loads a whole catalog or a whole result set.
3. **Safety lives in the database.** A client-side check is a seatbelt, not a lock (§5).
4. **Capabilities, not inheritance trees.** Engines differ a lot (Redis has no schema; Mongo explains differently). Use a flags enum, the same idiom as `LanguageCapabilities`, plus optional interfaces.
5. **Ship a vertical slice first,** then widen. Each milestone has a measurable "done when".

---

## 3. Architecture

### 3.1 Placement
- `Services/Database/` holds the engine-neutral contracts, profiles, history and the safety guard. It has **no Avalonia types**, so it moves to `FrySharp.Core` in [Idea.md](Idea.md) Phase 0 without changes.
- Providers live in `Services/Database/Providers/<Engine>/`. On-demand drivers load in an isolated context (§9).
- UI: a Data side bar view, the Results tab and a status bar widget, registered through the extension descriptors.
- **Web mode is the preferred surface** ([Idea.md](Idea.md)). Every database action goes through Core and is exposed as `fry/db/*` methods on the session protocol (`connect`, `children`, `execute` streaming `QueryEvent`s, `cancel`, `explain`, `applyChanges`). The browser then gets the same features as the desktop, with the web's virtualized table renderer (Idea.md W5) as its results grid. Secrets never reach the browser: the server resolves them from `ISecretStore`.

### 3.2 Contracts (revised)

```csharp
[Flags]
public enum DatabaseCapabilities
{
    None = 0,
    Schema = 1 << 0,            // GetChildrenAsync returns a tree
    Transactions = 1 << 1,      // BeginAsync / Commit / Rollback
    Explain = 1 << 2,           // ExplainAsync
    EditableResults = 1 << 3,   // results from a single keyed table can be edited
    MultipleResultSets = 1 << 4,
    Parameters = 1 << 5,        // @p / :p / $1 binding
    ServerSideCancel = 1 << 6,  // cancelling stops work on the server, not just the reader
    ReadOnlySession = 1 << 7,   // the engine can enforce read-only for the session (see §5)
}

public enum EnvironmentTier { Development, Staging, Production }

public interface IDatabaseProvider
{
    string ProviderId { get; }                      // "sqlite", "postgres", "sqlserver", "mysql", "duckdb", "mongodb", "redis"
    string DisplayName { get; }
    DatabaseCapabilities Capabilities { get; }
    IReadOnlyList<ConnectionFieldDescriptor> Fields { get; }
    string QueryLanguageId { get; }                 // "sql" for relational; "mongo" / "redis" get their own languages later

    Task<IDatabaseSession> OpenAsync(ConnectionProfile profile, ISecretStore secrets, CancellationToken ct);
}

public interface IDatabaseSession : IAsyncDisposable
{
    ConnectionProfile Profile { get; }
    string ServerVersion { get; }                   // shown in the status bar: "PostgreSQL 17.2"

    // Lazy tree: null parent = roots. Large levels are paged.
    Task<SchemaPage> GetChildrenAsync(SchemaNode? parent, string? pageToken, CancellationToken ct);

    // Streams events as they arrive: ResultSetStarted(columns) → RowsChunk(rows) … → ResultSetEnded(rowsAffected, elapsed) → Message/Notice → Completed.
    // The token cancels; providers with ServerSideCancel also stop the server work.
    IAsyncEnumerable<QueryEvent> ExecuteAsync(QueryRequest request, CancellationToken ct);
}

// Optional, checked with `session is IExplainable e`:
public interface IExplainable  { Task<ExecutionPlan> ExplainAsync(string query, bool analyze, CancellationToken ct); }
public interface ITransactional { bool InTransaction { get; } Task BeginAsync(CancellationToken ct); Task CommitAsync(CancellationToken ct); Task RollbackAsync(CancellationToken ct); }
```

Changes from the first draft, and why:
- **`GetSchemaHierarchyAsync` → `GetChildrenAsync(parent, pageToken)`.** Loading the full hierarchy breaks on big databases and blocks the first paint.
- **Materialized `QueryBatchResult` → `IAsyncEnumerable<QueryEvent>`.** The first rows appear right away, memory stays bounded, and multiple result sets and server notices arrive in order.
- **`CancelActiveQueryAsync` removed.** One `CancellationToken` is the cancel path. Npgsql turns it into a PostgreSQL cancel request; each provider's behaviour is tested (§10).
- **`IRelationalSession` / `INoSqlSession` split dropped.** The draft's diagram and code disagreed, and capabilities cover the differences.
- **Staged grid edits are separate from transactions.** The draft's `ITransactionManager.StagedModificationsCount` mixed them. Edits are a client-side `ChangeSet` on the result model. "Apply" runs the generated statements in one transaction where the engine supports it.

### 3.3 One SQL lexer
`SqlSyntaxHighlighting` is an AvaloniaEdit XSHD rule set and can't be reused as a tokenizer, and `SqlCompletionService` uses regexes. Add one small, dialect-aware lexer in `Services/Database/` that understands strings, quoted identifiers, `--` and `/* */` comments, and PostgreSQL dollar quoting. Four features share it:
- the statement splitter (`Ctrl+Enter` runs the statement at the caret, and multi-statement batches),
- parameter detection (`@p`, `:p`, `$1`, but not inside strings or comments),
- the safety classifier (§5),
- completion context ("after `FROM`", "after `alias.`").

Its cost per keystroke must track the edited statement, not the document size.

### 3.4 Schema cache (for the tree, completion and code generation)
- Built in the background per connection, level by level, and cancellable. It can be refreshed per node.
- Completion asks the cache and **never waits on the network** while the user types. If a name isn't loaded yet, completion offers what is loaded and asks for the rest in the background.
- Persist an optional snapshot per profile (with a timestamp) so a cold start on a huge schema has completion at once.

---

## 4. UI in the 5-zone layout

| Zone | What appears | Notes |
|---|---|---|
| 1 Activity Bar | **Data** icon (`Database`), badge = open connections | Single-sidebar rule: one Data view with sections, not several side bars |
| 2 Side Bar | Sections: **Connections** (grouped by tier) → lazy schema tree; **Query History**; right-click actions (New Query, Select Top 100, Generate C#, Copy Name) | Uses `LazyExplorerTree`. A paged level shows "Load 500 more" |
| 3 Editor | `.sql` tabs with a connection picker in the editor toolbar. A **Production banner** when the tier is Production. A parameter bar when the query has parameters | The connection comes from `-- :connection name` in the file, else from the picker |
| 4 Bottom Deck | The existing **Results** tab gains sub-tabs per result set (`Result 1 · 14 rows`), plus **Messages** and later **Plan** | Reuse the Results tab. No second results deck |
| 5 Status Bar | Tier badge + `user@host/db`, transaction state, `Rows: 500 of 14,200 · 8 ms` | Shown only while a SQL document or SQL cell is active. The 22px bar has little room |

**Keybindings** (scoped to SQL editors, checked against the existing keymap):
- `F5`: run the file or selection (unchanged meaning).
- `Ctrl+Enter`: run the **statement at the caret** (free in the Code Studio editor today).
- `Shift+F5` / `Esc` in Results: cancel.
- Grid edits: an **Apply** button opens the Review SQL dialog. **Not `Ctrl+S`**, which is Save everywhere. If a shortcut is wanted, choose one the keymap doesn't use and add it to the shortcuts list.

---

## 5. Production safety (corrected)

The first draft relied on "inspecting the query AST" and "rejecting all non-SELECT statements". Neither is reliable on its own:
- `SELECT` can change data (`SELECT pg_terminate_backend(…)`, functions with side effects), and PostgreSQL allows `WITH d AS (DELETE …) SELECT …`.
- A regex over raw text is fooled by comments, strings and dollar-quoting, and a real AST needs a parser per dialect.

**Layered design:**
1. **Read-only enforced by the engine where possible** (`ReadOnlySession` capability):
   - SQLite: open with `Mode=ReadOnly`. This is a real guarantee.
   - PostgreSQL: `Options=-c default_transaction_read_only=on`. A user statement can turn it off again, so this is a guard, not a guarantee.
   - MySQL/MariaDB: `SET SESSION TRANSACTION READ ONLY` after connecting. Same caveat.
   - SQL Server: there is no session switch (`ApplicationIntent=ReadOnly` only routes to replicas).
   - **Docs and the connection dialog say so plainly:** the real protection for production is a **read-only database role**. The dialog offers a "connect as a read-only user" hint for Production profiles.
2. **Client-side statement classifier (the seatbelt):** tokenize with comments, strings, quoted identifiers and dollar quotes removed, split statements, and flag `DROP`, `TRUNCATE`, `ALTER`, `GRANT`/`REVOKE`, and `DELETE`/`UPDATE` without `WHERE`. On Production, a flagged statement asks the user to **type the database name**. On Staging, a plain confirm. On Development, nothing.
3. **Visual chrome:** a Production banner in the editor, a crimson status-bar badge, a tinted Results header. Use theme tokens, not hard-coded colours (theme studio rule).
4. **Grid edits on Production** are always reviewed and never auto-committed.

---

## 6. Credentials and connection profiles

- **Profiles** (no secrets) live in `connections.json`, at user level or in the workspace (`.frysharp/connections.json`, safe to commit). Values can use `${env:PG_PASSWORD}` for teams and CI.
- **Secrets** go through one new `ISecretStore`:
  - macOS: Keychain, through the `security` CLI or Security.framework P/Invoke.
  - Windows: DPAPI (`System.Security.Cryptography.ProtectedData`, a Windows-only package).
  - Linux: Secret Service via `secret-tool`/libsecret. If that is missing, **say so** and offer "don't save the password" rather than writing it in plain text silently.
- **Move the AI API key into `ISecretStore` in the same milestone.** It is in plain text today.
- SSH tunnels (SSH.NET) come later (M4): a profile option, with the key passphrase in `ISecretStore`.

---

## 7. Notebooks (`#!sql` with connections)

What needs building: options on the language directive line, a connection-bound SQL kernel, and a typed share.

```csharp
// Cell 1: SQL against a named connection; the result is kept as "customersTable"
#!sql --connection dev-postgres --as customersTable
SELECT id, company_name, revenue, country
FROM customers
WHERE active
ORDER BY revenue DESC
LIMIT 100;
```

```csharp
// Cell 2: C#. Today a shared SQL table arrives as List<object> of Dictionary<string, object> rows.
#!csharp
#!share --from sql customersTable --as customers

var top10 = customers.Cast<Dictionary<string, object>>()
    .Select(r => (Name: (string)r["company_name"], Revenue: Convert.ToDouble(r["revenue"])))
    .Take(10)
    .ToList();

top10.Dump();
Display.BarChart(top10.ToDictionary(x => x.Name, x => x.Revenue), "Top 10 customers by revenue");
```

- Without `--connection`, cells keep today's behaviour: the per-notebook temp SQLite database.
- `--as` caps how many rows are kept (default 10,000, configurable) and says when it truncates. A share never silently drops rows.
- **Typed share (M3):** when the source is a known table or query, share into C# as a generated `record` list (or `DataTable`, which `KernelValueSharing` already handles going the other way). Then `customers[0].Revenue` is a `decimal` with completion, which ties into code generation (§8).

---

## 8. Schema → C# code generation

1. **Record / POCO** with nullable reference types from column nullability. Type map per engine (`uuid`→`Guid`, `timestamptz`→`DateTimeOffset`, `numeric`→`decimal`, `jsonb`→`JsonDocument`). Optional `System.Text.Json` attributes. The project builds with `LangVersion 13`, so generated code must not use C# 14 features.
2. **Dapper repository** (`GetByIdAsync`, `ListAsync`, `InsertAsync`, `UpdateAsync`) with parameters, never string concatenation.
3. **EF Core** entity + `DbContext` with fluent configuration (keys, FKs, column types).
4. **Insert into active tab / cell**, or open in a new `.frycs` tab. Generation reads from the schema cache, so it is instant and works offline.
5. Golden-file tests per engine type map.

---

## 9. Drivers: bundled vs. on demand

FrySharp ships as a FryPDF plugin, so every bundled megabyte reaches every user. Some drivers carry native binaries per platform.

| Tier | Engine | Package | Notes |
|---|---|---|---|
| Bundled | SQLite | `Microsoft.Data.Sqlite` (+ SQLitePCLRaw native `e_sqlite3`) | **Removes the sqlite3 CLI requirement.** Check that the FryPDF host doesn't load a different SQLitePCLRaw first (load-context conflict). |
| Bundled | PostgreSQL | `Npgsql` (managed) | |
| On demand | SQL Server | `Microsoft.Data.SqlClient` | Has native and auth dependencies, so load it in its own context |
| On demand | MySQL / MariaDB | `MySqlConnector` (managed) | Small, so it could be bundled. Decide by measured plugin size |
| On demand | DuckDB | `DuckDB.NET.Data.Full` (large native library) | Great for Parquet/CSV analysis in notebooks; a strong candidate after PostgreSQL |
| Later (M6) | MongoDB, Redis | `MongoDB.Driver`, `StackExchange.Redis` | Different query models, so they need their own design (§12) |
| Unscheduled | Oracle, Snowflake, Elasticsearch, Cassandra, Neo4j, LiteDB | — | Only when users ask. Each one is a maintenance commitment |

On-demand drivers download through `NuGetReferenceResolver` into the managed package cache and load in an isolated `AssemblyLoadContext` (pattern: `ExtensionLoadContext`). Show progress and size before downloading, and make it work offline once cached.

---

## 10. Milestones (each with "done when")

### M1 — Vertical slice: SQLite in-process + PostgreSQL
- [ ] `Services/Database/` contracts (§3.2), `ConnectionProfile`, `ISecretStore` (the AI key moves too).
- [ ] SQLite provider on `Microsoft.Data.Sqlite`. `SqlScriptRunner` and `SqlNotebookKernel` use it; the sqlite3 CLI stays only as a fallback. **Decision in §11.**
- [ ] PostgreSQL provider on Npgsql.
- [ ] Connection dialog: fields or URI, tier, "Test connection" with latency.
- [ ] Data side bar: connections → lazy, paged schema tree.
- [ ] Run file / selection / statement at caret, **streamed** into the Results tab with result-set sub-tabs, Messages, cancel, and "Fetch more" past the first page.
- [ ] Status bar: tier badge, connection, row count, elapsed.
- [ ] `-- :connection <name>` in files; `#!sql --connection <name> --as <var>` in notebooks.

**Done when** (measured with `tools/UiSnapshots perf`, plus tests):
- `SELECT` over **1,000,000 rows**: first rows show within 300 ms of the server's first byte, the UI thread is never blocked for more than 50 ms, and memory stays under a fixed cap (the grid pages; it doesn't hold a million rows).
- A schema with **50,000 tables**: the tree's first level shows within 200 ms, and expanding a node never lists the whole catalog.
- Cancelling a running `pg_sleep(60)` returns within 1 s, and the server-side query is gone (checked in `pg_stat_activity`).
- SQL runs on a machine **without sqlite3 installed**.
- Tests run against **real engines**: SQLite always, in-process. PostgreSQL through a local server or Docker when one is available, otherwise the tests are reported as skipped (never silently passing).

### M2 — Safety, history, parameters, export
- [ ] §5 in full: engine read-only, statement classifier with tests for comments/strings/CTEs, Production confirmations and chrome.
- [ ] Query history (local SQLite): text, profile, duration, rows, status. Search, reopen, **retention limit, and a per-profile "don't record"** (query text can contain personal data or secrets).
- [ ] Parameter bar for `@p`, `:p` and `$1`, with typed inputs and values remembered per document.
- [ ] Export: CSV/TSV, JSON/JSON Lines, Markdown, SQL `INSERT`. **Excel only if a dependency (e.g. ClosedXML) is accepted.** Optional: export to PDF through the host, a natural fit for FryPDF.

### M3 — Code generation, typed share, grid editing
- [ ] §8 generators with golden tests.
- [ ] Typed `#!share` from SQL (§7).
- [ ] Grid editing only when the result set comes from **one table with a primary key** (otherwise read-only, with the reason shown). The `ChangeSet` diff is coloured with theme tokens. The Review SQL dialog shows the exact parameterized statements, and Apply runs them in one transaction.

### M4 — More engines and connectivity
- [ ] On-demand driver store (§9).
- [ ] SQL Server, MySQL/MariaDB, DuckDB providers.
- [ ] SSH tunnels.

### M5 — Plans and diagrams
- [ ] `EXPLAIN` / `EXPLAIN ANALYZE` parsed from PostgreSQL JSON, SQL Server showplan XML and MySQL `FORMAT=JSON` into one `ExecutionPlan` tree, with hot nodes and estimate-vs-actual gaps marked.
- [ ] ER diagram. **No node-link diagram renderer exists in the studio today.** Ship text first (Mermaid `erDiagram` / DBML export, cheap and useful in docs), then pick a renderer (e.g. a layout library in the existing WebView) as its own design task.

### M6 — Document and key-value stores
- [ ] Separate design doc first: query languages (`#!mongo` as a new language id with its own editor support; Redis command console), result shapes (document tree view), and what "schema" means (sampled fields).

---

## 11. Decisions for the owner

1. **SQLite engine:** replace the sqlite3 CLI with in-process `Microsoft.Data.Sqlite` (recommended: works with nothing installed, typed results, streaming)? Or keep the CLI as the default? Dot-commands like `.tables` and `.import` only exist in the CLI, so keep the CLI as an opt-in for them.
2. **Results grid:** extend `DumpTableView` (recommended: already virtualized and themed) or add Avalonia's `DataGrid` / `TreeDataGrid` package (more built-in editing, more dependency and styling work)?
3. **Bundle size:** bundle MySqlConnector or download it on demand?
4. **Excel export:** accept a library dependency, or ship CSV and let Excel open it?
5. **Priority vs. web mode ([Idea.md](Idea.md)):** build M1's engine half (contracts, providers, `ISecretStore`) straight into `FrySharp.Core` right after Phase 0 slice 0a, and the UI on desktop and web together. Or finish web W1–W4 first and add databases after?

## 12. Risks

| Risk | Mitigation |
|---|---|
| Scope creep (9+ engines, diagrams, plans, editing) | Engines in tiers. M1 is the only commitment until it meets its numbers. |
| Native driver conflicts inside the FryPDF host | Isolated load contexts. Test the plugin inside the real host, not only in the standalone `Runner`. |
| False sense of safety on Production | Say plainly in the UI and docs that read-only roles are the real protection. Test the classifier against tricky SQL. |
| Huge results freezing the UI or using memory | Streaming + paging + a row cap with "Fetch more". The 1M-row check is in the perf harness. |
| Secrets leaking through history or exports | Per-profile history opt-out, retention limits, no secrets in profiles, `${env:…}` support. |
| NoSQL contorting the relational design | Capabilities + optional interfaces. NoSQL gets its own design pass (M6). |
