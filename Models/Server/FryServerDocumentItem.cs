namespace PdfEditorApp.Plugins.CSharpEditor.Models.Server;

/// <summary>
/// Root document model for a .fryserver file.
/// </summary>
public class FryServerDocumentItem
{
    public string SchemaVersion { get; set; } = "1.0";
    public string Type { get; set; } = "server";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled Server";
    public string Description { get; set; } = string.Empty;
    public FryServerConfiguration ServerConfig { get; set; } = new();
    public List<FryServerCellItem> Cells { get; set; } = new();
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
