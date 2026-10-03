using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Lsp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;

/// <summary>
/// A dynamic language definition synthesized at runtime from an extension's declarative manifest.
/// Supports syntax highlighting (.xshd), CLI compilation/execution runners, and LSP integration.
/// </summary>
public sealed class DeclarativeLanguageDefinition : LanguageDefinition
{
    private readonly DeclarativeLanguageContribution _contribution;
    private readonly string _extensionDirectory;
    private readonly Lazy<IHighlightingDefinition?> _highlighting;

    public override string Id => _contribution.Id;
    public override string DisplayName => _contribution.DisplayName;
    public override string ShortName => !string.IsNullOrWhiteSpace(_contribution.ShortName)
        ? _contribution.ShortName
        : _contribution.Id.ToUpperInvariant();

    public override IReadOnlyList<string> FileExtensions => _contribution.Extensions.Count > 0
        ? _contribution.Extensions
        : [$"." + _contribution.Id.ToLowerInvariant()];

    public override IReadOnlyList<string> Aliases => _contribution.Aliases;
    public override string IconKind => !string.IsNullOrWhiteSpace(_contribution.IconKind) ? _contribution.IconKind : "CodeBraces";
    public override string AccentHex => !string.IsNullOrWhiteSpace(_contribution.AccentHex) ? _contribution.AccentHex : "#8B949E";
    public override string LineCommentPrefix => !string.IsNullOrWhiteSpace(_contribution.LineCommentPrefix) ? _contribution.LineCommentPrefix : "//";
    public override string NewFileTemplate => _contribution.NewFileTemplate ?? $"{LineCommentPrefix} {_contribution.DisplayName}\n";
    public override string RuntimeDescription => _contribution.RuntimeDescription ?? _contribution.DisplayName;
    public override bool IsCompiled => _contribution.IsCompiled || !string.IsNullOrWhiteSpace(_contribution.BuildCommand);

    public override LanguageCapabilities Capabilities
    {
        get
        {
            var caps = LanguageCapabilities.None;
            if (!string.IsNullOrWhiteSpace(_contribution.RunCommand))
            {
                caps |= LanguageCapabilities.StandardInput;
            }
            if (_contribution.Lsp != null && !string.IsNullOrWhiteSpace(_contribution.Lsp.Command))
            {
                caps |= LanguageCapabilities.Completion | LanguageCapabilities.QuickInfo | LanguageCapabilities.LiveDiagnostics;
            }
            return caps;
        }
    }

    public override IScriptRunner? ScriptRunner { get; }
    public override IEditorAssistantFactory? EditorAssistants { get; }

    public DeclarativeLanguageDefinition(DeclarativeLanguageContribution contribution, string extensionDirectory)
    {
        _contribution = contribution ?? throw new ArgumentNullException(nameof(contribution));
        _extensionDirectory = extensionDirectory ?? throw new ArgumentNullException(nameof(extensionDirectory));

        _highlighting = new Lazy<IHighlightingDefinition?>(LoadHighlightingDefinition);

        if (!string.IsNullOrWhiteSpace(contribution.RunCommand) || !string.IsNullOrWhiteSpace(contribution.BuildCommand))
        {
            ScriptRunner = new DeclarativeScriptRunner(contribution, extensionDirectory);
        }

        if (contribution.Lsp != null && !string.IsNullOrWhiteSpace(contribution.Lsp.Command))
        {
            EditorAssistants = new GenericLspEditorAssistantFactory(contribution.Lsp.Command, contribution.Lsp.Args, contribution.Id);
        }
    }

    public override IHighlightingDefinition? GetHighlighting(bool isDark) => _highlighting.Value;

    private IHighlightingDefinition? LoadHighlightingDefinition()
    {
        if (string.IsNullOrWhiteSpace(_contribution.SyntaxFile)) return null;

        string syntaxPath = Path.IsPathRooted(_contribution.SyntaxFile)
            ? _contribution.SyntaxFile
            : Path.Combine(_extensionDirectory, _contribution.SyntaxFile);

        if (!File.Exists(syntaxPath)) return null;

        try
        {
            using var stream = File.OpenRead(syntaxPath);
            using var reader = XmlReader.Create(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch
        {
            return null;
        }
    }

    private sealed class DeclarativeScriptRunner : IScriptRunner
    {
        private readonly DeclarativeLanguageContribution _contrib;
        private readonly string _extDir;

        public DeclarativeScriptRunner(DeclarativeLanguageContribution contrib, string extDir)
        {
            _contrib = contrib;
            _extDir = extDir;
        }

        public Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
        {
            var steps = new List<ProcessStep>();

            if (!string.IsNullOrWhiteSpace(_contrib.BuildCommand))
            {
                var buildSpec = ParseCommand(_contrib.BuildCommand, context);
                steps.Add(new ProcessStep($"Building {_contrib.DisplayName}", buildSpec, IsBuildStep: true));
            }

            if (!string.IsNullOrWhiteSpace(_contrib.RunCommand))
            {
                var runSpec = ParseCommand(_contrib.RunCommand, context);
                steps.Add(new ProcessStep($"Running {_contrib.DisplayName}", runSpec, IsBuildStep: false));
            }

            return Task.FromResult(new ScriptRunPlan(steps));
        }

        private static ProcessStartSpec ParseCommand(string template, ScriptRunContext context)
        {
            string formatted = template
                .Replace("{file}", context.SourceFilePath)
                .Replace("{filePath}", context.SourceFilePath)
                .Replace("{fileName}", Path.GetFileName(context.SourceFilePath))
                .Replace("{workDir}", context.WorkingDirectory);

            var tokens = SplitArgs(formatted);
            string exe = tokens.Count > 0 ? tokens[0] : template;
            var args = tokens.Count > 1 ? tokens.Skip(1).ToArray() : Array.Empty<string>();

            return new ProcessStartSpec
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = context.WorkingDirectory
            };
        }

        private static List<string> SplitArgs(string command)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(command)) return list;

            bool inQuotes = false;
            char quoteChar = '\0';
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];
                if ((c == '"' || c == '\'') && !inQuotes)
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                }
                else if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        list.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                list.Add(current.ToString());
            }

            return list;
        }
    }
}
