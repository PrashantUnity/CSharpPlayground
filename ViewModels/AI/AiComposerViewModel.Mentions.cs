using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;

public partial class AiComposerViewModel
{
    [ObservableProperty]
    private bool _isMentionPopupOpen;

    public ObservableCollection<MentionSuggestionItem> MentionSuggestions { get; } = new();
    public ObservableCollection<MentionSuggestionItem> FilteredMentions { get; } = new();

    private void InitializeMentions()
    {
        MentionSuggestions.Clear();
        MentionSuggestions.Add(new MentionSuggestionItem
        {
            Prefix = "@Files",
            Title = "Workspace Files",
            Description = "Attach full list of files and folders in active workspace",
            IconKind = "FolderMultipleOutline",
            Category = "Context"
        });
        MentionSuggestions.Add(new MentionSuggestionItem
        {
            Prefix = "@Problems",
            Title = "Compiler Diagnostics",
            Description = "Attach live Roslyn compiler errors and warnings from Problems panel",
            IconKind = "AlertCircleOutline",
            Category = "Context"
        });
        MentionSuggestions.Add(new MentionSuggestionItem
        {
            Prefix = "@Docs",
            Title = "Studio API & SDK",
            Description = "Attach API signatures for App.UI, App.Theme, App.Commands, App.Editor",
            IconKind = "BookOpenOutline",
            Category = "Context"
        });
        MentionSuggestions.Add(new MentionSuggestionItem
        {
            Prefix = "@UI",
            Title = "Studio Visual Tree",
            Description = "Inspect running studio Avalonia visual tree and mounted slots",
            IconKind = "ViewDashboardOutline",
            Category = "Studio Freedom"
        });
        MentionSuggestions.Add(new MentionSuggestionItem
        {
            Prefix = "@Theme",
            Title = "Studio Theme & Styles",
            Description = "Discover active color tokens and modify live studio styling",
            IconKind = "PaletteOutline",
            Category = "Studio Freedom"
        });
        MentionSuggestions.Add(new MentionSuggestionItem
        {
            Prefix = "@Widget",
            Title = "Inject Studio Widget",
            Description = "Inject declarative XAML widget into EditorToolbar, SideBar, or BottomDeck",
            IconKind = "PuzzleOutline",
            Category = "Studio Freedom"
        });
        MentionSuggestions.Add(new MentionSuggestionItem
        {
            Prefix = "@Selection",
            Title = "Active Selection",
            Description = "Attach currently highlighted code snippet and caret position",
            IconKind = "CursorDefaultClickOutline",
            Category = "Context"
        });

        ResetFilteredMentions();
    }

    private void ResetFilteredMentions()
    {
        FilteredMentions.Clear();
        foreach (var item in MentionSuggestions)
        {
            FilteredMentions.Add(item);
        }
    }

    partial void OnPromptTextChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            IsMentionPopupOpen = false;
            return;
        }

        int atIdx = value.LastIndexOf('@');
        if (atIdx >= 0 && (atIdx == 0 || char.IsWhiteSpace(value[atIdx - 1])))
        {
            string query = value[(atIdx + 1)..];
            if (query.Contains(' ') || query.Contains('\n') || query.Contains('\r'))
            {
                IsMentionPopupOpen = false;
                return;
            }

            FilteredMentions.Clear();
            var matches = MentionSuggestions
                .Where(m => m.Prefix.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            m.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var match in matches)
            {
                FilteredMentions.Add(match);
            }

            // Also search workspace files if query is 2+ chars
            if (query.Length >= 2 && _toolRegistry != null)
            {
                try
                {
                    var fileListing = _toolRegistry.ListWorkspaceFiles(pattern: $"*{query}*");
                    if (!string.IsNullOrWhiteSpace(fileListing) && !fileListing.StartsWith("Error"))
                    {
                        var lines = fileListing.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Where(l => !l.StartsWith("Workspace root") && !l.StartsWith("Directories") && !l.StartsWith("Files"))
                            .Take(4);

                        foreach (var f in lines)
                        {
                            var cleanFile = f.Trim();
                            if (!string.IsNullOrWhiteSpace(cleanFile))
                            {
                                FilteredMentions.Add(new MentionSuggestionItem
                                {
                                    Prefix = $"@{Path.GetFileName(cleanFile)}",
                                    Title = Path.GetFileName(cleanFile),
                                    Description = cleanFile,
                                    IconKind = "FileDocumentOutline",
                                    Category = "Files"
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore search errors in background
                }
            }

            IsMentionPopupOpen = FilteredMentions.Count > 0;
        }
        else
        {
            IsMentionPopupOpen = false;
        }
    }

    [RelayCommand]
    public void InsertMention(string mention)
    {
        if (string.IsNullOrWhiteSpace(mention)) return;

        string current = PromptText ?? string.Empty;
        int atIdx = current.LastIndexOf('@');
        if (atIdx >= 0)
        {
            PromptText = current[..atIdx] + mention + " ";
        }
        else if (string.IsNullOrWhiteSpace(current))
        {
            PromptText = mention + " ";
        }
        else
        {
            PromptText = current.TrimEnd() + " " + mention + " ";
        }

        IsMentionPopupOpen = false;
    }

    private string EnrichContextWithMentions(string userPrompt, string? baseContext)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(baseContext))
        {
            sb.AppendLine(baseContext);
        }

        if (_toolRegistry == null) return sb.ToString();

        if (userPrompt.Contains("@Files", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("=== Workspace Files (@Files) ===");
            sb.AppendLine(_toolRegistry.ListWorkspaceFiles());
        }

        if (userPrompt.Contains("@Problems", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("=== Compiler Diagnostics (@Problems) ===");
            sb.AppendLine(_toolRegistry.GetDiagnostics());
        }

        if (userPrompt.Contains("@Docs", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("=== Studio API Documentation (@Docs) ===");
            sb.AppendLine(_toolRegistry.GetStudioApiMetadata("all"));
        }

        if (userPrompt.Contains("@UI", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("=== Studio Live Visual Tree (@UI) ===");
            sb.AppendLine(_toolRegistry.InspectStudioUi(4));
        }

        if (userPrompt.Contains("@Theme", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("=== Studio Theme Tokens & Extensions (@Theme) ===");
            sb.AppendLine(_toolRegistry.GetRuntimeExtensionPoints());
        }

        // Generic @<filename> mentions (e.g. @Script_215216.frycs, @Calculator.cs, etc.)
        var fileMentions = System.Text.RegularExpressions.Regex.Matches(userPrompt, @"@([A-Za-z0-9_\-\.]+)");
        foreach (System.Text.RegularExpressions.Match match in fileMentions)
        {
            var token = match.Groups[1].Value;
            if (token.Equals("Files", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Problems", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Docs", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("UI", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Theme", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Widget", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Selection", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var activeDocContext = _toolRegistry.GetActiveFileContext();
            if (activeDocContext.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine();
                sb.AppendLine($"=== Targeted Active Document: @{token} ===");
                sb.AppendLine("Instruction: The user explicitly targeted this active document. When modifying or adding code, provide the updated code in a ```csharp code block.");
                continue;
            }

            try
            {
                var content = _toolRegistry.ReadFile(token);
                if (!content.StartsWith("Error", StringComparison.OrdinalIgnoreCase) &&
                    !content.StartsWith("File not found", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine();
                    sb.AppendLine($"=== Mentioned File: @{token} ===");
                    sb.AppendLine(content);
                }
            }
            catch
            {
                // Tolerate unresolvable token
            }
        }

        return sb.ToString();
    }

    [RelayCommand]
    public void PresetCustomizeTheme()
    {
        PromptText = "Please customize the studio theme to a modern sleek midnight blue palette with glowing cyan accents and apply it to App.Theme.";
    }

    [RelayCommand]
    public void PresetInjectWidget()
    {
        PromptText = "Inject a live system memory monitor widget into the EditorToolbar slot using inject_studio_widget.";
    }

    [RelayCommand]
    public void PresetInspectUi()
    {
        PromptText = "Inspect the running studio visual tree and describe the visible hierarchy and contribution slots.";
    }
}
