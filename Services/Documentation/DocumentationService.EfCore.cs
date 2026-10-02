using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildEfCoreCategory()
    {
        return new DocCategory
        {
            Id = "ef_core",
            Title = "Entity Framework Core",
            IconKind = MaterialIconKind.DatabaseOutline,
            AccentColor = "#818CF8",
            Badge = "ORM",
            Description = "Entity Framework Core: modeling data with DbContext, migrations, and LINQ-to-SQL queries.",
            Articles = new List<DocArticle>
            {
                CreateEfCoreArticle()
            }
        };
    }

    private DocArticle CreateEfCoreArticle()
    {
        return new DocArticle
        {
            Id = "learn_ef_core",
            Title = "Entity Framework Core: DbContext, Migrations & LINQ",
            Subtitle = "Model your data with DbContext and DbSet, evolve the schema with migrations, and query it with LINQ instead of hand-written SQL.",
            ReadingTime = "8 min read",
            Summary = "Entity Framework Core (EF Core) is Microsoft's object-relational mapper for .NET: it lets you model a database as plain C# classes, generate and apply schema changes through migrations, and query with LINQ, which EF Core translates into SQL. This chapter covers the core building blocks and the change-tracking behavior every EF Core developer eventually needs to understand.",
            Keywords = new List<string> { "ef core", "entity framework", "dbcontext", "dbset", "migrations", "linq to sql", "code first", "database first", "change tracking", "asnotracking", "modelbuilder", "orm" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "DbContext & DbSet",
                    Content = "A `DbContext` represents a session with the database: it tracks the connection, configuration, and an in-memory cache of entities you've loaded or added during its lifetime. Each `DbSet<T>` property on your context maps to a table (or view) and gives you a LINQ-queryable, trackable collection of that entity type.",
                    BulletPoints = new List<string>
                    {
                        "A DbContext is meant to be short-lived — typically one per unit of work (e.g. one per web request via dependency injection).",
                        "DbSet<T> supports Add, Remove, Update, Find, and full LINQ querying.",
                        "Register the context with a provider in DI, e.g. `services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString))`."
                    }
                },
                new()
                {
                    Heading = "Code-First vs Database-First",
                    Content = "EF Core supports two complementary starting points. In Code-First, you write C# entity classes first and let EF Core generate migrations that create/evolve the database schema to match. In Database-First, an existing database is scaffolded (`dotnet ef dbcontext scaffold`) into a matching DbContext and entity classes.",
                    BulletPoints = new List<string>
                    {
                        "Code-First — best when the application owns the schema and you want the model and migrations to be the source of truth.",
                        "Database-First — best when integrating with an existing or externally-owned database.",
                        "Both approaches produce the same runtime shape: a DbContext with DbSet<T> properties and a mapped model."
                    }
                },
                new()
                {
                    Heading = "Migrations",
                    Content = "Migrations are the mechanism EF Core uses to evolve your database schema alongside your C# model in a Code-First workflow. Running `dotnet ef migrations add <Name>` compares your current model to the last snapshot and generates a migration class with `Up`/`Down` methods; `dotnet ef database update` applies pending migrations to the target database.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Review generated migrations before applying them to a production database — EF Core can occasionally infer a destructive change (like a column drop/recreate) that you'd rather express as a safer, hand-edited step."
                },
                new()
                {
                    Heading = "LINQ to SQL",
                    Content = "Querying a DbSet<T> with LINQ (`Where`, `Select`, `OrderBy`, `Include`, ...) builds an expression tree that EF Core translates into SQL for the underlying provider, rather than executing in memory. Execution is deferred until the query is enumerated — with `ToList()`, `ToListAsync()`, `FirstOrDefault()`, a `foreach`, and so on.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Use `Include(...)` to eagerly load related entities in the same query — without it, navigation properties are left unpopulated (or lazily loaded, if you've opted into that) and accessing them can trigger extra round-trips."
                },
                new()
                {
                    Heading = "Change Tracking",
                    Content = "By default, every entity a DbContext returns from a query is attached to its change tracker, which records each property's original and current value. Calling `SaveChanges()`/`SaveChangesAsync()` compares tracked entities against their original values and generates the minimal INSERT/UPDATE/DELETE statements needed — you rarely write SQL by hand.",
                    BulletPoints = new List<string>
                    {
                        "Entities can be Added, Unchanged, Modified, Deleted, or Detached — `context.Entry(entity).State` exposes this directly.",
                        "Modifying a tracked entity's property is enough for EF Core to include it in the next SaveChanges — no explicit \"Update\" call needed for already-tracked entities.",
                        "The change tracker has real memory/CPU cost, which matters for large read-heavy queries (see Performance Optimization below)."
                    }
                },
                new()
                {
                    Heading = "Performance Optimization",
                    Content = "Change tracking is the main lever for EF Core performance tuning. Read-only queries — ones you'll never call SaveChanges on — don't need entities to be tracked at all, so skipping it avoids the bookkeeping cost entirely.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Add `.AsNoTracking()` to any query whose results you only read and never modify — it's one of the single cheapest wins available for report/listing endpoints and large read queries."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "DbContext.SaveChanges / SaveChangesAsync", ReturnType = "int / Task<int>", Parameters = "", Description = "Persists all tracked Added/Modified/Deleted entities to the database in one transaction and returns the number of affected rows." },
                new() { MethodName = "DbSet<T>.Add", ReturnType = "EntityEntry<T>", Parameters = "T entity", Description = "Begins tracking the given entity in the Added state; it is inserted on the next SaveChanges call." },
                new() { MethodName = "ModelBuilder.Entity<T>", ReturnType = "EntityTypeBuilder<T>", Parameters = "", Description = "Starts fluent configuration for entity type T inside OnModelCreating — keys, indexes, relationships, column mappings." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_efcore_dbcontext_query",
                    Title = "DbContext, DbSet & a LINQ Query",
                    Description = "Illustrates a DbContext with a DbSet and a filtering/ordering LINQ query. Conceptual/illustrative — requires a project referencing Microsoft.EntityFrameworkCore and a provider package (e.g. Microsoft.EntityFrameworkCore.SqlServer); not runnable directly in this app's script runner without those packages and a real database.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    public class AppDbContext : DbContext
                    {
                        public DbSet<Customer> Customers => Set<Customer>();
                        public DbSet<Order> Orders => Set<Order>();

                        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
                            options.UseSqlServer("Server=.;Database=Shop;Trusted_Connection=True;");
                    }

                    public class Customer
                    {
                        public int Id { get; set; }
                        public string Name { get; set; } = string.Empty;
                        public List<Order> Orders { get; set; } = new();
                    }

                    public class Order
                    {
                        public int Id { get; set; }
                        public int CustomerId { get; set; }
                        public decimal Total { get; set; }
                        public DateTime PlacedAt { get; set; }
                    }

                    await using var db = new AppDbContext();

                    var recentBigOrders = await db.Orders
                        .Where(o => o.Total > 100 && o.PlacedAt > DateTime.UtcNow.AddDays(-30))
                        .OrderByDescending(o => o.PlacedAt)
                        .Include(o => o.Customer)
                        .ToListAsync();

                    foreach (var order in recentBigOrders)
                    {
                        Console.WriteLine($"Order #{order.Id}: {order.Total:C} placed {order.PlacedAt:d}");
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_efcore_fluent_configuration",
                    Title = "Fluent Configuration with OnModelCreating",
                    Description = "Illustrates fluent-API model configuration (keys, required columns, relationships) inside OnModelCreating. Conceptual/illustrative — requires a project referencing Microsoft.EntityFrameworkCore; not runnable directly in this app's script runner.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    public class AppDbContext : DbContext
                    {
                        public DbSet<Customer> Customers => Set<Customer>();
                        public DbSet<Order> Orders => Set<Order>();

                        protected override void OnModelCreating(ModelBuilder modelBuilder)
                        {
                            modelBuilder.Entity<Customer>(entity =>
                            {
                                entity.HasKey(c => c.Id);
                                entity.Property(c => c.Name)
                                      .IsRequired()
                                      .HasMaxLength(200);
                            });

                            modelBuilder.Entity<Order>(entity =>
                            {
                                entity.HasKey(o => o.Id);
                                entity.Property(o => o.Total).HasColumnType("decimal(10,2)");

                                entity.HasOne<Customer>()
                                      .WithMany(c => c.Orders)
                                      .HasForeignKey(o => o.CustomerId)
                                      .OnDelete(DeleteBehavior.Cascade);
                            });
                        }
                    }

                    public class Customer
                    {
                        public int Id { get; set; }
                        public string Name { get; set; } = string.Empty;
                        public List<Order> Orders { get; set; } = new();
                    }

                    public class Order
                    {
                        public int Id { get; set; }
                        public int CustomerId { get; set; }
                        public decimal Total { get; set; }
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_efcore_asnotracking",
                    Title = "Change Tracking vs AsNoTracking()",
                    Description = "Illustrates the difference between a tracked query (used when you plan to modify and SaveChanges) and a no-tracking query (used for read-only results). Conceptual/illustrative — requires a project referencing Microsoft.EntityFrameworkCore; not runnable directly in this app's script runner.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    await using var db = new AppDbContext();

                    // Tracked: EF Core watches this entity so a later SaveChanges knows what changed.
                    var order = await db.Orders.FirstAsync(o => o.Id == 42);
                    order.Total += 10m;
                    await db.SaveChangesAsync(); // generates an UPDATE for the modified column(s) only

                    // Read-only: no change tracking overhead, ideal for reports/listings you'll never save.
                    var summaries = await db.Orders
                        .AsNoTracking()
                        .Select(o => new { o.Id, o.Total, o.PlacedAt })
                        .ToListAsync();

                    Console.WriteLine($"Loaded {summaries.Count} read-only order summaries.");
                    """
                }
            }
        };
    }
}
