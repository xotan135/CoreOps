namespace NetworkAdmin.App.Models;

public sealed record class MaintenanceResult
{
    public string ComputerName { get; init; } = "";
    public string State { get; init; } = "Unknown";
    public string Item { get; init; } = "";
    public string Message { get; init; } = "";
}
