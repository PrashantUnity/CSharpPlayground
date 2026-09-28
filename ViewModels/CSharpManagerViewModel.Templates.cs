using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpManagerViewModel
{
    [ObservableProperty]
    private string _selectedTemplateCategory = "All";

    public ObservableCollection<string> TemplateCategories { get; } = new()
    {
        "All", "Algorithms", "Visualizers", "Data Structures", "Notebooks", "Scripts"
    };

    public bool IsAllTemplateCategoryActive => SelectedTemplateCategory == "All";
    public bool IsAlgorithmsTemplateCategoryActive => SelectedTemplateCategory == "Algorithms";
    public bool IsVisualizersTemplateCategoryActive => SelectedTemplateCategory == "Visualizers";
    public bool IsDataStructuresTemplateCategoryActive => SelectedTemplateCategory == "Data Structures";
    public bool IsNotebooksTemplateCategoryActive => SelectedTemplateCategory == "Notebooks";
    public bool IsScriptsTemplateCategoryActive => SelectedTemplateCategory == "Scripts";

    partial void OnSelectedTemplateCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllTemplateCategoryActive));
        OnPropertyChanged(nameof(IsAlgorithmsTemplateCategoryActive));
        OnPropertyChanged(nameof(IsVisualizersTemplateCategoryActive));
        OnPropertyChanged(nameof(IsDataStructuresTemplateCategoryActive));
        OnPropertyChanged(nameof(IsNotebooksTemplateCategoryActive));
        OnPropertyChanged(nameof(IsScriptsTemplateCategoryActive));
        RefreshFilteredTemplates();
    }

    [RelayCommand]
    public void SetSelectedTemplateCategory(string category)
    {
        SelectedTemplateCategory = category;
    }

    [RelayCommand]
    public void ClearSelectedTemplate()
    {
        SelectedTemplate = null;
    }

    [RelayCommand]
    public void ResetTemplateFilters()
    {
        SearchQuery = string.Empty;
        SelectedTemplateCategory = "All";
    }

    public int MatchingTemplatesCount => ScriptTemplates.Count() + NotebookTemplates.Count();
    public bool HasMatchingTemplates => MatchingTemplatesCount > 0;

    public void RefreshFilteredTemplates()
    {
        OnPropertyChanged(nameof(ScriptTemplates));
        OnPropertyChanged(nameof(NotebookTemplates));
        OnPropertyChanged(nameof(MatchingTemplatesCount));
        OnPropertyChanged(nameof(HasMatchingTemplates));
    }

    public IEnumerable<CodeTemplate> FilterTemplates(IEnumerable<CodeTemplate> templates)
    {
        var category = SelectedTemplateCategory;
        var query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        return templates.Where(t =>
        {
            if (category == "Scripts" && t.IsNotebook) return false;
            if (category == "Notebooks" && !t.IsNotebook) return false;
            if (category == "Visualizers" && !IsVisualizerTemplate(t)) return false;
            if (category == "Algorithms" && !IsAlgorithmTemplate(t)) return false;
            if (category == "Data Structures" && !IsDataStructureTemplate(t)) return false;

            if (string.IsNullOrEmpty(query)) return true;

            return t.Title.ToLowerInvariant().Contains(query) ||
                   t.Description.ToLowerInvariant().Contains(query) ||
                   t.Category.ToLowerInvariant().Contains(query) ||
                   t.KindBadgeText.ToLowerInvariant().Contains(query) ||
                   t.Tags.Any(tag => tag.ToLowerInvariant().Contains(query));
        });
    }

    private static bool IsVisualizerTemplate(CodeTemplate t)
    {
        if (t.Category.Contains("Visualiz", StringComparison.OrdinalIgnoreCase) ||
            t.Category.Equals("Charting", StringComparison.OrdinalIgnoreCase) ||
            t.Category.Equals("Animation", StringComparison.OrdinalIgnoreCase) ||
            t.Category.Equals("Graphics", StringComparison.OrdinalIgnoreCase))
            return true;

        if (t.Title.Contains("Visualiz", StringComparison.OrdinalIgnoreCase) ||
            t.Title.Contains("Playback", StringComparison.OrdinalIgnoreCase) ||
            t.Title.Contains("Stepping", StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains("Visualiz", StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains("Playback", StringComparison.OrdinalIgnoreCase))
            return true;

        return t.Tags.Any(tag =>
            tag.Contains("Visualiz", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Tracker", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Charts", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Canvas", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Animation", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Bars", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDataStructureTemplate(CodeTemplate t)
    {
        if (t.Category.Equals("Data Structures", StringComparison.OrdinalIgnoreCase) ||
            t.Category.Equals("Data Science", StringComparison.OrdinalIgnoreCase))
            return true;

        return t.Tags.Any(tag =>
            tag.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Graph", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("List", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Stack", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Queue", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Matrix", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Array", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("DataFrame", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Table", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAlgorithmTemplate(CodeTemplate t)
    {
        if (t.Category.Equals("Algorithms", StringComparison.OrdinalIgnoreCase))
            return true;

        return t.Tags.Any(tag =>
            tag.Contains("Algorithm", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Sort", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Search", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("DP", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("BFS", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("DFS", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Recursion", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Two-Sum", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Two-Pointer", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Pathfinding", StringComparison.OrdinalIgnoreCase));
    }
}
