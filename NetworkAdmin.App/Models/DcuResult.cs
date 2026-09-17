namespace NetworkAdmin.App.Models;

public sealed record class DcuResult
{
    public string ComputerName { get; init; } = "";
    public string State { get; init; } = "Unknown";
    public string Version { get; init; } = "";
    public int? UpdateCount { get; init; }
    public string Updates { get; init; } = "";
    public bool RebootRequired { get; init; }
    public int? ExitCode { get; init; }
    public string Message { get; init; } = "";
    public string UpdateCountDisplay => UpdateCount?.ToString() ?? "—";
    public string RebootDisplay => RebootRequired ? "Required" : "No";
}
