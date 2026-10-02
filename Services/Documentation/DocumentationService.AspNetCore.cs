using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildAspNetCoreCategory()
    {
        return new DocCategory
        {
            Id = "aspnet_core",
            Title = "ASP.NET Core",
            IconKind = MaterialIconKind.WebBox,
            AccentColor = "#4ADE80",
            Badge = "Web",
            Description = "Building web APIs and applications with ASP.NET Core: middleware, routing, and minimal APIs.",
            Articles = new List<DocArticle>
            {
                CreateAspNetCoreArticle()
            }
        };
    }

    private DocArticle CreateAspNetCoreArticle()
    {
        return new DocArticle
        {
            Id = "learn_aspnet_core",
            Title = "ASP.NET Core: Middleware, Routing & Minimal APIs",
            Subtitle = "Learn how a request flows through the ASP.NET Core pipeline, and how to expose it as minimal APIs or MVC controllers.",
            ReadingTime = "8 min read",
            Summary = "ASP.NET Core is the cross-platform framework behind most modern .NET web apps and APIs. This chapter walks through the middleware pipeline, the choice between Minimal APIs and MVC, routing, model binding and validation, authentication/authorization, and filters — the building blocks you'll reach for in any real web project.",
            Keywords = new List<string> { "aspnet", "asp.net core", "middleware", "minimal api", "mvc", "web api", "routing", "model binding", "validation", "authentication", "authorization", "filters", "webapplication", "endpoint" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Middleware — The Request Pipeline",
                    Content = "Every incoming HTTP request travels through a chain of middleware components, each of which can inspect or modify the request, short-circuit the pipeline, or pass control to the next component with `await next(context)`. Order matters: middleware registered earlier with `app.Use...` wraps everything registered after it, like nested layers of an onion.",
                    BulletPoints = new List<string>
                    {
                        "Built-in middleware includes routing, authentication, authorization, static files, CORS, and exception handling.",
                        "Custom middleware is just a delegate — `app.Use(async (context, next) => { ... await next(context); ... })`.",
                        "A middleware that never calls `next` terminates the pipeline for that request (short-circuiting)."
                    },
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Register exception-handling and HTTPS-redirection middleware near the top of the pipeline so they can wrap everything that follows."
                },
                new()
                {
                    Heading = "MVC vs Minimal APIs",
                    Content = "ASP.NET Core offers two main programming models for building HTTP endpoints. Minimal APIs map a route directly to a delegate with almost no ceremony — ideal for small services and microservices. MVC (Model-View-Controller) organizes endpoints into controller classes with actions, giving you conventions, filters, and richer tooling that scale better for larger applications.",
                    BulletPoints = new List<string>
                    {
                        "Minimal APIs — endpoints defined with `app.MapGet`/`MapPost`/etc. directly in Program.cs (or grouped extension methods); minimal boilerplate.",
                        "MVC Controllers — classes deriving from `ControllerBase` (or `Controller` for views), decorated with `[ApiController]` and route attributes.",
                        "Both share the same routing, model binding, dependency injection, and middleware pipeline underneath."
                    }
                },
                new()
                {
                    Heading = "Web API Fundamentals",
                    Content = "A Web API built with ASP.NET Core exposes resources over HTTP, typically exchanging JSON. `[ApiController]` opts a controller into API-specific conventions: automatic 400 responses on invalid model state, attribute routing requirement, and inference of binding sources (route, query, body) for action parameters.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Return `IActionResult`/`ActionResult<T>` (or, in minimal APIs, `Results.Ok(...)`/`TypedResults`) so you can express different status codes — 200, 201, 404, 400 — from the same action."
                },
                new()
                {
                    Heading = "Routing",
                    Content = "Routing matches an incoming request's URL and HTTP method to an endpoint. Minimal APIs register routes with `MapGet`, `MapPost`, `MapPut`, `MapDelete`, etc. on `IEndpointRouteBuilder`. MVC controllers instead use attribute routing (`[Route(\"api/[controller]\")]`, `[HttpGet(\"{id}\")]`) or, less commonly today, conventional routing configured centrally.",
                    BulletPoints = new List<string>
                    {
                        "Route parameters like `{id}` are bound to matching method parameters automatically.",
                        "Route constraints (`{id:int}`) restrict which values a segment accepts.",
                        "Endpoint groups (`app.MapGroup(\"/api/orders\")`) let you share a route prefix, filters, and metadata across related endpoints."
                    }
                },
                new()
                {
                    Heading = "Model Binding & Validation",
                    Content = "Model binding takes values from the route, query string, headers, form, or JSON body and populates action parameters or a bound model object. Data annotations (`[Required]`, `[Range]`, `[StringLength]`) declare validation rules directly on the model; with `[ApiController]`, an invalid model automatically produces a 400 Bad Request with a problem-details body before your action code even runs.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Data annotations validate shape (required, length, range) but not business rules — still check things like \"does this order belong to this customer\" explicitly in your handler."
                },
                new()
                {
                    Heading = "Authentication & Authorization",
                    Content = "Authentication answers \"who is this caller?\" (e.g. via JWT bearer tokens, cookies, or an identity provider); authorization answers \"are they allowed to do this?\". Configure authentication services with `AddAuthentication(...)` and authorization with `AddAuthorization(...)`, then wire the corresponding middleware into the pipeline with `app.UseAuthentication()` and `app.UseAuthorization()` — in that order, before endpoints are mapped.",
                    BulletPoints = new List<string>
                    {
                        "`[Authorize]` on a controller/action or minimal-API endpoint requires an authenticated (and optionally role/policy-matching) caller.",
                        "`[AllowAnonymous]` opts a specific endpoint out of an otherwise-required authentication policy.",
                        "Policies can express fine-grained rules beyond simple roles, e.g. `options.AddPolicy(\"AdultsOnly\", p => p.RequireClaim(\"age\", \"18+\"))`."
                    }
                },
                new()
                {
                    Heading = "Filters",
                    Content = "Filters run at specific points in the MVC action-invocation pipeline — before/after model binding, before/after the action executes, or when an exception is thrown. They're a good place for cross-cutting concerns that need MVC context (like the action arguments), such as logging, caching, or custom validation, as an alternative to writing global middleware.",
                    BulletPoints = new List<string>
                    {
                        "Authorization filters — run first, decide whether the request is allowed to proceed.",
                        "Action filters (`IActionFilter`) — wrap the action method itself (`OnActionExecuting`/`OnActionExecuted`).",
                        "Exception filters — handle unhandled exceptions thrown by an action, similar to a scoped try/catch."
                    }
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "WebApplication.CreateBuilder", ReturnType = "WebApplicationBuilder", Parameters = "string[] args", Description = "Creates a pre-configured builder with default logging, configuration, and dependency-injection setup for a web app." },
                new() { MethodName = "IApplicationBuilder.Use", ReturnType = "IApplicationBuilder", Parameters = "Func<HttpContext, RequestDelegate, Task> middleware", Description = "Adds a middleware delegate to the request pipeline that can act before and after calling the next component." },
                new() { MethodName = "IEndpointRouteBuilder.MapGet", ReturnType = "RouteHandlerBuilder", Parameters = "string pattern, Delegate handler", Description = "Registers an endpoint that handles HTTP GET requests matching the given route pattern." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_aspnet_minimal_api",
                    Title = "Minimal API — Program.cs with Custom Middleware",
                    Description = "Illustrates a minimal-API Program.cs with a couple of routes and a custom logging middleware. This is conceptual/illustrative — it targets an ASP.NET Core Web project (SDK Microsoft.NET.Sdk.Web, referencing Microsoft.AspNetCore.App) and will not run directly in this app's Roslyn script runner.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    var builder = WebApplication.CreateBuilder(args);
                    var app = builder.Build();

                    // Custom middleware: logs every request's method + path before continuing the pipeline.
                    app.Use(async (context, next) =>
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {context.Request.Method} {context.Request.Path}");
                        await next(context);
                    });

                    app.MapGet("/", () => "Hello from Minimal APIs!");

                    app.MapGet("/products/{id:int}", (int id) =>
                        id > 0
                            ? Results.Ok(new { Id = id, Name = $"Product {id}" })
                            : Results.NotFound());

                    app.MapPost("/products", (Product product) =>
                        Results.Created($"/products/{product.Id}", product));

                    app.Run();

                    record Product(int Id, string Name, decimal Price);
                    """
                },
                new()
                {
                    Id = "snip_learn_aspnet_mvc_controller",
                    Title = "MVC Controller — Model Binding & Validation",
                    Description = "Illustrates an [ApiController]-based controller action with data-annotation validation. Conceptual/illustrative — requires an ASP.NET Core Web API project referencing Microsoft.AspNetCore.App; not runnable directly in this app's script runner.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    [ApiController]
                    [Route("api/[controller]")]
                    public class OrdersController : ControllerBase
                    {
                        private readonly List<Order> _orders = new();

                        [HttpGet("{id:int}")]
                        public ActionResult<Order> GetById(int id)
                        {
                            var order = _orders.FirstOrDefault(o => o.Id == id);
                            return order is null ? NotFound() : Ok(order);
                        }

                        [HttpPost]
                        public ActionResult<Order> Create([FromBody] CreateOrderRequest request)
                        {
                            // [ApiController] already returned 400 automatically if data annotations failed,
                            // so by the time we get here the model is known to be valid.
                            var order = new Order(_orders.Count + 1, request.CustomerName, request.Total);
                            _orders.Add(order);
                            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
                        }
                    }

                    public record Order(int Id, string CustomerName, decimal Total);

                    public class CreateOrderRequest
                    {
                        [Required, StringLength(100, MinimumLength = 1)]
                        public string CustomerName { get; set; } = string.Empty;

                        [Range(0.01, 100000)]
                        public decimal Total { get; set; }
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_aspnet_authorize",
                    Title = "Protecting an Endpoint with [Authorize]",
                    Description = "Illustrates requiring authentication (and a role) on a minimal-API endpoint. Conceptual/illustrative — requires authentication services configured in an ASP.NET Core project (e.g. JWT bearer via Microsoft.AspNetCore.Authentication.JwtBearer); not runnable directly in this app's script runner.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    var builder = WebApplication.CreateBuilder(args);

                    builder.Services.AddAuthentication("Bearer").AddJwtBearer();
                    builder.Services.AddAuthorization(options =>
                    {
                        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                    });

                    var app = builder.Build();

                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.MapGet("/public/status", () => "OK");

                    app.MapGet("/admin/dashboard", () => "Welcome, admin.")
                       .RequireAuthorization("AdminOnly");

                    app.Run();
                    """
                }
            }
        };
    }
}
