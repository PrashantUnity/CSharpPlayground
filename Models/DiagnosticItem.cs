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
        DiagnosticSeverity.Error => "#FFB4AB",
        DiagnosticSeverity.Warning => "#FBBF24",
        DiagnosticSeverity.Info => "#A8C7FA",
        _ => "#9BA1AD"
    };
}
