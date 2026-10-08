# Universal Database Integration Plan

> **Objective**: Equip C# Code Studio (**FrySharp**) with an authentic, high-performance universal database engineering suite. Developers can connect to any **RDBMS** (PostgreSQL, MySQL, SQLite, SQL Server, DuckDB) or **NoSQL** data store (MongoDB, Redis, Elasticsearch, LiteDB), explore schemas, interactively design and edit data inline, generate strongly typed C# POCOs/EF Core models, execute queries directly inside the editor and polyglot notebooks (`#!sql`, `#!mongo`), and visualize execution plans and ER diagrams—all within the 5-zone VS Code ergonomic layout.

---

## 1. Architectural Vision & 5-Zone VS Code Integration

In strict alignment with the **5-Zone VS Code Ergonomics Mandate**, database capabilities integrate natively into the existing studio chrome:

```
┌────┬──────────────────────┬──────────────────────────────────────────────────────────────┐
│ A  │   DATA EXPLORER      │                         EDITOR AREA                          │
│ C  │                      │                                                              │
│ T  │  ▼ Connections       │  [Tab: Query.sql ×]  [Tab: Schema.er]  [Notebook: App.csnb]  │
│ I  │    ▶ [DEV] Local PG  ├──────────────────────────────────────────────────────────────┤
│ V  │    ▼ [PROD] AWS RDS  │  🔴 [PRODUCTION SAFE MODE - READ ONLY]                       │
│ I  │      ▶ customers     │  SELECT id, company, revenue FROM customers WHERE id = @cid; │
│ T  │      ▶ orders        ├──────────────────────────────────────────────────────────────┤
│ Y  │    ▶ [DEV] Mongo DB  │  [Parameter Bar]  @cid: [ 42091 ]  [▶ Run (Cmd+Enter)]       │
│    │  ▼ Query History     ├──────────────────────────────────────────────────────────────┤
│ B  │    ⏱ 2m ago (14ms)   │                     BOTTOM PANEL / DOCK                      │
│ A  │    ⏱ 1h ago (320ms)  │  [RESULTS #1] [RESULTS #2] [EXPLAIN PLAN] [JSON] [HISTORY]   │
│ R  │  ▼ ER Diagram        │  (Virtualized grid, inline CRUD editing, staged SQL commit)  │
├────┴──────────────────────┴──────────────────────────────────────────────────────────────┤
│                                  STATUS BAR (22px fixed)                                 │
│ >< C# Studio  🔴 [PROD] RDS PG 16  Tx: Manual (1 staged)  Ln 1, Col 1  Rows: 1,420 (8ms) 
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

### 5-Zone Role Breakdown:
- **Zone 1: Activity Bar**: Dedicated Database rail icon (`DatabaseOutline` / `ServerOutline`) with badge counters for active connections or uncommitted transactions.
- **Zone 2: Primary Side Bar**: 
  - **Connection Profiles Tree**: Grouped by environment tags (`[DEV]`, `[STAGE]`, `[PROD]`).
  - **Schema Explorer**: Drill down into Catalogs $\rightarrow$ Schemas $\rightarrow$ Tables $\rightarrow$ Columns, Keys, Indexes, Functions, and Collections.
  - **Query History Deck**: Chronological log of recent executions with execution times, row counts, and one-click reload.
  - **Contextual Code Actions**: Right-click table $\rightarrow$ "Generate C# Record / POCO", "Generate Dapper Query", "Generate EF Core Model", "Show ER Diagram".
- **Zone 3: Editor Canvas**:
  - Full `.sql` and `.mql` (MongoDB Query Language) editor with syntax highlighting, code folding, and auto-completion.
  - **Parameter Bar (`@param` / `:param`)**: Auto-detected query parameters rendered as an interactive form bar directly above the editor.
  - **Production Safe Mode Banner**: Highly visible crimson banner for Production connections preventing accidental destructive executions.
  - **Visual ER Diagram Canvas**: Interactive graph view showing table relationships, cardinalities, and foreign keys.
  - **Polyglot Notebook Integration**: `#!sql` and `#!mongo` cell magics with seamless cross-language sharing (`#!share --from sql myTable --as df`).
- **Zone 4: Bottom Tool Deck**:
  - **Multi-Tab Results Grid**: High-performance virtualized `DataGrid` supporting multiple simultaneous result sets (`SELECT 1; SELECT 2;`).
  - **Inline CRUD Data Editor**: Double-click cell editing, row additions, and deletions with staged diffs and reviewable SQL `COMMIT` dialog.
  - **Visual Execution Plan**: Graphical tree breakdown of `EXPLAIN (ANALYZE, BUFFERS)` showing expensive sequential scans and index bottlenecks.
  - **JSON Document Tree Inspector**: Collapsible tree viewer for MongoDB BSON documents and JSON column payloads.
- **Zone 5: Status Bar**:
  - Environment badge (🟢 `[DEV]`, 🟡 `[STAGE]`, 🔴 `[PROD]`).
  - Active Connection & Database name (`postgres@aws-rds:5432 / billing_db`).
  - Transaction status indicator (`Tx: Auto-Commit` or `Tx: Manual (2 staged changes)`).
  - Execution stopwatch (`⏱ 14ms`) and fetched row count (`Rows: 500 / 14,200`).

---

## 2. Core Architecture & Extensible Provider Engine

To support both relational SQL and document/key-value NoSQL engines without code bloat, the architecture separates connection lifecycle, schema discovery, data editing, and query execution into isolated interfaces.

```
                              ┌────────────────────────┐
                              │   IDatabaseProvider    │
                              └───────────┬────────────┘
                                          │
                         ┌────────────────┴────────────────┐
                         ▼                                 ▼
              ┌─────────────────────┐           ┌─────────────────────┐
              │  IRelationalSession │           │    INoSqlSession    │
              └──────────┬──────────┘           └──────────┬──────────┘
                         │                                 │
          ┌──────────────┼──────────────┐           ┌──────┴──────┐
          ▼              ▼              ▼           ▼             ▼
       Npgsql     MySqlConnector     SQLite       MongoDB       Redis
      (Postgres)     (MySQL)                    (BSON/MQL)  (Commands)
```

### Core Interface Definitions

```csharp
public enum DatabaseCategory
{
    Relational,
    Document,
    KeyValue,
    WideColumn,
    SearchEngine
}

public enum EnvironmentTier
{
    Development,
    Staging,
    Production
}

public interface IDatabaseProvider
{
    string ProviderId { get; }          // e.g. "postgres", "mysql", "sqlite", "mongodb", "redis"
    string DisplayName { get; }
    DatabaseCategory Category { get; }
    IReadOnlyList<ConnectionFieldDescriptor> RequiredParameters { get; }

    Task<IDatabaseSession> ConnectAsync(ConnectionProfile profile, CancellationToken ct);
    Task<ConnectionTestResult> TestConnectionAsync(ConnectionProfile profile, CancellationToken ct);
}

public interface IDatabaseSession : IAsyncDisposable
{
    string SessionId { get; }
    ConnectionProfile Profile { get; }
    ConnectionState State { get; }
    ITransactionManager TransactionManager { get; }

    Task<IReadOnlyList<SchemaNode>> GetSchemaHierarchyAsync(CancellationToken ct);
    Task<QueryBatchResult> ExecuteQueryAsync(string queryText, QueryExecutionOptions options, CancellationToken ct);
    Task<ExecutionPlanResult?> ExplainQueryAsync(string queryText, CancellationToken ct);
    Task CancelActiveQueryAsync();
}
```

### Transaction Management (`ITransactionManager`)

```csharp
public interface ITransactionManager
{
    bool IsAutoCommitEnabled { get; set; }
    bool HasActiveTransaction { get; }
    int StagedModificationsCount { get; }

    Task BeginTransactionAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}
```

---

## 3. C# Superpower: Schema-to-C# Code Generation

As an authentic C# IDE, FrySharp provides instant code scaffolding directly from the database schema:

```
┌───────────────────────────────────────────────┐
│ Database Explorer:                            │
│   ▼ tables                                    │
│     ▶ customers  ───► Right Click Context:    │
│     ▶ orders         ├── Generate C# Record   │
│                      ├── Generate Dapper Repo │
│                      ├── Generate EF Core     │
│                      └── Inject into Tab      │
└───────────────────────────────────────────────┘
```

### Scaffolding Capabilities:
1. **C# 13 Record / POCO Generator**:
   - Maps database column data types (`uuid` $\rightarrow$ `Guid`, `timestamptz` $\rightarrow$ `DateTimeOffset`, `numeric` $\rightarrow$ `decimal`, `jsonb` $\rightarrow$ `JsonDocument` or typed POCO).
   - Generates nullable reference types based on database nullability flags (`string?`, `int?`).
   - Supports attributes: `System.Text.Json`, `Newtonsoft.Json`, and `Dapper.Contrib`.
2. **Dapper Repository Scaffolder**:
   - Generates type-safe asynchronous query methods (`GetByIdAsync`, `ListAsync`, `InsertAsync`, `UpdateAsync`).
3. **Entity Framework Core Entity & `DbContext` Scaffolder**:
   - Generates fluent API configuration (`OnModelCreating`) with table mappings, column types, primary keys, and foreign key relationships.
4. **"Inject Into Active Tab" Action**:
   - One-click insertion directly into the open C# script (`.csx`), FryCS document, or notebook cell without needing manual copy-pasting.

---

## 4. Inline Grid Data Editing & Safe CRUD Operations

Developers can manipulate table data without writing manual `INSERT`, `UPDATE`, or `DELETE` statements:

1. **Inline Cell Editing**:
   - Double-click any cell to modify values inline with type validation (dates, integers, booleans, GUIDs).
   - Multi-line text and JSON modal editor for complex structures.
2. **Staged Change Diffing**:
   - Edited rows are highlighted in **Yellow** (Modified).
   - Newly inserted rows are highlighted in **Green** (Inserted).
   - Marked-for-deletion rows are highlighted in **Red** (Deleted).
3. **"Review SQL & Commit" Modal**:
   - Clicking **Apply Changes** (`Ctrl+S` / `Cmd+S`) displays a formatted preview of the exact generated SQL statements:
     ```sql
     -- Staged Changes Preview (2 operations)
     UPDATE customers SET email = 'jane.doe@example.com' WHERE id = 104;
     DELETE FROM orders WHERE id = 8991;
     ```
   - Developers review and confirm the statements before executing them against the target database.

---

## 5. Production "Safe Mode" & Environment Governance

Accidental data loss on production instances is prevented through active safeguards:

1. **Environment Tiers**:
   - Each connection profile is tagged: **Development** (Green), **Staging** (Amber), or **Production** (Crimson).
2. **Visual Warning Chrome**:
   - When connected to Production, the status bar badge, editor breadcrumbs, and results deck render high-visibility crimson accent borders.
3. **Destructive Query Interceptor**:
   - Automatically inspects outgoing query ASTs.
   - Any query containing `DROP`, `TRUNCATE`, `ALTER`, `DELETE` (without `WHERE`), or `UPDATE` (without `WHERE`) triggers an explicit confirmation dialog requiring the user to type the database name to confirm execution.
4. **Read-Only Enforced Mode**:
   - Toggleable flag on connection profiles that rejects all non-`SELECT` statements before sending them over the wire.

---

## 6. Interactive Visual ER Diagrams

Leveraging FrySharp's existing visual canvas architecture (`Charting`, `Visuals`):

1. **Automatic Foreign Key Graphing**:
   - Analyzes schema relationships and foreign key constraints to build an interactive node-and-link graph.
   - Nodes display table names, primary keys (🔑), column types, and nullability.
   - Edges display relationship cardinalities ($1:1$, $1:N$, $N:M$) and foreign key constraints (`ON DELETE CASCADE`).
2. **Interactive Controls**:
   - Smooth pan and zoom with mini-map overview.
   - Isolate table: Focus on a selected table and dim all unrelated tables.
   - Export diagram to SVG, PNG, or Markdown Mermaid format.

---

## 7. Execution Plans & Multi-Result Sets

1. **Multiple Result Sets**:
   - Supports multi-statement batches (`SELECT * FROM users; SELECT * FROM products;`).
   - Renders each result set in an independent numbered sub-tab in the Bottom Tool Deck (`Result 1 (14 rows)`, `Result 2 (80 rows)`).
2. **Visual Execution Plan (EXPLAIN ANALYZE)**:
   - Evaluates PostgreSQL JSON execution plans, SQL Server XML execution plans, and MySQL explain data.
   - Graphically renders the query plan tree with visual indicators:
     - High-cost nodes (e.g. Seq Scan on 1,000,000 rows highlighted in red).
     - Actual vs. estimated row discrepancies.
     - Buffer hit ratios and index scan efficiency.

---

## 8. Query History, Parameters & Export Engine

1. **Query History Log**:
   - Automatically tracks every executed query in an isolated SQLite local log.
   - Metadata recorded: timestamp, connection profile, duration (ms), row count, and execution status (success / error).
   - Filter history by text, date, or connection name; one-click "Open in Editor".
2. **Interactive Parameter Bar**:
   - Automatically parses `@param` (SQL Server, MySQL, SQLite) or `:param` / `$1` (Postgres, Oracle).
   - Generates input fields in a floating parameter bar above the editor with typed inputs.
3. **Export Engine**:
   - One-click export of any result set to:
     - **CSV / TSV** (custom delimiter, headers, quoted strings)
     - **JSON / JSON Lines**
     - **Excel (`.xlsx`)** (with formatted headers and auto-column width)
     - **Markdown Table** (ready for documentation)
     - **SQL `INSERT INTO` statements** (for data migration)

---

## 9. On-Demand Dynamic Driver Resolution (Driver Store)

To prevent bloating the base application binary with hundreds of megabytes of third-party drivers:
- **Core Drivers Bundled**: Lightweight essential drivers (SQLite, PostgreSQL, MySQL) are bundled natively.
- **On-Demand NuGet Resolution**: Specialized drivers (Oracle `Oracle.ManagedDataAccess.Core`, SQL Server `Microsoft.Data.SqlClient`, Snowflake, Cassandra, Neo4j) are dynamically downloaded via NuGet into the studio's managed package cache when first requested.
- **Isolated `AssemblyLoadContext`**: Drivers load into isolated contexts to prevent dependency conflicts with the IDE host runtime.

---

## 10. Supported Database Ecosystem

### Relational / ADO.NET Engines
| Engine | Driver Package | Feature Coverage |
|---|---|---|
| **PostgreSQL** | `Npgsql` | Catalogs, Schemas, JSONB, Arrays, Functions, EXPLAIN ANALYZE |
| **SQLite** | `Microsoft.Data.Sqlite` | Embedded file & in-memory databases, zero-configuration |
| **MySQL / MariaDB** | `MySqlConnector` | Catalogs, Tables, Views, Stored Procedures, Explain |
| **SQL Server** | `Microsoft.Data.SqlClient` | Schemas, Temp tables, XML/JSON columns, Execution Plans |
| **DuckDB** | `DuckDB.NET.Data` | Embedded analytical query engine directly over Parquet, CSV, and JSON |

### NoSQL & Non-Relational Engines
| Engine | Driver Package | Interaction Model |
|---|---|---|
| **MongoDB** | `MongoDB.Driver` | Collections, BSON queries, Aggregation pipelines, Document tree |
| **Redis** | `StackExchange.Redis` | Keyspace inspection, Strings, Hashes, Lists, Sets, TTLs, Live Command REPL |
| **Elasticsearch** | `Elastic.Clients.Elasticsearch` | Indices, Mappings, Lucene/DSL queries, Cluster health |
| **LiteDB** | `LiteDB` | Embedded document database for rapid desktop prototyping |

---

## 11. Polyglot Notebook Integration (`#!sql` Magic)

Engineers combine direct database querying with C# data analysis and rich charting:

```csharp
// Cell 1: Query database directly into notebook memory
#!sql --connection dev-postgres --as customersTable
SELECT id, company_name, revenue, country 
FROM customers 
WHERE active = true 
ORDER BY revenue DESC;

// Cell 2: Share and analyze seamlessly in C# (.NET 10)
#!csharp
#!share --from sql customersTable --as customers

var top10 = customers.Rows
    .Select(r => new { Name = r["company_name"], Revenue = (decimal)r["revenue"] })
    .Take(10);

Display.Table(top10);
Display.BarChart(top10.Select(x => x.Name), top10.Select(x => x.Revenue));
```

---

## 12. Security & Credential Protection

- **Master Credential Vault**: Passwords and connection tokens are never saved in plain-text `.json` or workspace configs.
- **OS Platform Keychain**:
  - **macOS**: Apple Keychain Services via Security framework.
  - **Windows**: Windows Data Protection API (DPAPI / `ProtectedData`).
  - **Linux**: Freedesktop Secret Service API via D-Bus / SecretStorage.
- **Connection Environment Variables**: Support `$PG_PASSWORD`, `${ENV_VAR}` interpolation for team repositories and CI.
- **SSH Jump-Host Tunneling**: Built-in SSH forwarding (`SSH.NET`) for accessing internal private VPC databases securely.

---

## 13. Phased Implementation Roadmap

### Milestone 1: Core Engine & Relational Foundation
- [ ] Create `Services/Database/` domain in `CSharpPlayground`.
- [ ] Define `IDatabaseProvider`, `IDatabaseSession`, `ISchemaExplorer`, `IQueryExecutor`, `ITransactionManager`.
- [ ] Implement `AdoNetRelationalProvider` base class.
- [ ] Implement **SQLite** (`Microsoft.Data.Sqlite`) and **PostgreSQL** (`Npgsql`) providers.
- [ ] Connection profile manager with OS Keychain encryption and environment tier tags (`Dev`, `Stage`, `Prod`).

### Milestone 2: UI Controls & VS Code Tool Windows
- [ ] Add Database icon to `ActivityBarControl`.
- [ ] Create `DatabaseExplorerControl` in Primary Side Bar:
  - TreeView for connections, schemas, tables, collections, columns.
  - Context menu actions ("New Query", "Select Top 100", "Generate POCO", "Show ER Diagram").
- [ ] Create `ConnectionDialogView`:
  - Connection mode (Parameters vs. Connection String URI).
  - Environment tier selector (Dev / Stage / Prod).
  - SSH Tunneling configuration tab.
  - "Test Connection" button with ping/latency indicator.
- [ ] Create `DataGridResultsDeck`:
  - Virtualizing grid capable of handling 100,000+ rows smoothly.
  - Multi-tab support for multiple result sets.
  - Export actions: CSV, JSON, Excel, Markdown, SQL INSERT.

### Milestone 3: C# Code Generator & Data Editing
- [ ] Implement `ISchemaToCodeGenerator`:
  - C# 13 Record / POCO generator with accurate nullability and data type mapping.
  - Dapper query generator & EF Core model scaffolder.
  - "Inject into Active Tab" command.
- [ ] Implement inline CRUD data editing:
  - Double-click cell edit with staged change tracking.
  - "Review SQL & Commit" modal with generated `UPDATE` / `DELETE` / `INSERT` diff preview.
  - Status Bar transaction management badge (`Auto-Commit` vs `Manual`).

### Milestone 4: Safety & Developer Ergonomics
- [ ] Implement Production Safe Mode:
  - Visual crimson chrome indicators for `[PROD]` connections.
  - Destructive query interceptor with explicit confirmation modal.
  - Read-only connection profile enforcement.
- [ ] Implement Query History & Parameter Bar:
  - Local SQLite query history tracker with search and one-click reload.
  - Dynamic parameter bar above editor parsing `@param` and `:param`.

### Milestone 5: Visualizations & NoSQL Support
- [ ] Interactive Visual ER Diagram:
  - Foreign key relationship graph with zoom/pan and isolate table view.
- [ ] Visual Execution Plan (`EXPLAIN ANALYZE`):
  - Tree visualizer for PostgreSQL and SQL Server execution plans highlighting expensive nodes.
- [ ] NoSQL Providers:
  - **MongoDB** provider with BSON query runner and `JsonDocumentTreeViewer`.
  - **Redis** provider with keyspace browser and command console.
  - **DuckDB** provider for instant querying of local Parquet and CSV files.

### Milestone 6: Polyglot Notebook Integration & Dynamic Drivers
- [ ] Polyglot notebook `#!sql` and `#!mongo` kernel handlers.
- [ ] `#!share` integration into C# dataframes / dynamic rows.
- [ ] On-demand dynamic NuGet driver downloading (`IDriverPackageResolver`) via isolated `AssemblyLoadContext`.
