using Microsoft.CodeAnalysis;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class DiagnosticItem
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DiagnosticSeverity Severity { get; set; } = DiagnosticSeverity.Error;
    public int Line { get; set; } = 1;
    public int Column { get; set; } = 1;
    public int EndLine { get; set; } = 1;
    public int EndColumn { get; set; } = 1;

    public string LocationString => $"Line {Line}, Col {Column}";

    public string SeverityIconKind => Severity switch
    {
        DiagnosticSeverity.Error => "AlertCircleOutline",
        DiagnosticSeverity.Warning => "AlertOutline",
        DiagnosticSeverity.Info => "InformationOutline",
        _ => "HelpCircleOutline"
    };

    public string SeverityColorHex => Severity switch
    {
        DiagnosticSeverity.Error => "#EF4444",
        DiagnosticSeverity.Warning => "#F59E0B",
        DiagnosticSeverity.Info => "#3B82F6",
        _ => "#94A3B8"
    };
}
