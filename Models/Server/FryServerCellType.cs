namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// The functional type of a cell within a .fryserver document.
/// </summary>
public enum FryServerCellType
{
    /// <summary>HTTP route endpoint handler (GET, POST, PUT, DELETE, ANY).</summary>
    Endpoint,

    /// <summary>Runs on server start to seed shared in-memory data, caches, or singletons.</summary>
    Startup,

    /// <summary>Pre/post request pipeline interceptor (auth, headers, validation, rate limits).</summary>
    Middleware,

    /// <summary>Automated integration test scenario executing assertions against the live server.</summary>
    Scenario,

    /// <summary>Periodic background task/timer running while the server is active.</summary>
    Background,

    /// <summary>Markdown documentation, architecture notes, and API specifications.</summary>
    Markdown
}
