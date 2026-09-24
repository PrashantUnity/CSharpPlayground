using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildLearnCSharpCategory()
    {
        return new DocCategory
        {
            Id = "learn_csharp",
            Title = "Learn C#",
            IconKind = MaterialIconKind.LanguageCsharp,
            AccentColor = "#22D3EE",
            Badge = "Tutorials",
            Description = "Practical C# and .NET skills for everyday scripting: web requests, JSON, async programming, file I/O, and threading.",
            Articles = new List<DocArticle>
            {
                CreateHttpClientArticle(),
                CreateJsonArticle(),
                CreateAsyncArticle(),
                CreateFileIoArticle(),
                CreateThreadingArticle()
            }
        };
    }
}
