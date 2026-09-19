namespace NetworkAdmin.App.Models;

public sealed class AppSettings
{
    public string InventoryWorkbookPath { get; set; } = "";
    public List<string> ProtectedComputers { get; set; } = [];
    public double MainInputHeight { get; set; }
    public double TargetPaneRatio { get; set; }
    public double DcuTranscriptHeight { get; set; }
    public double DcuLogHeight { get; set; }
    public double LapsSearchPaneRatio { get; set; }
}
