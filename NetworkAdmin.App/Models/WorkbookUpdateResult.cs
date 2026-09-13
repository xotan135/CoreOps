namespace NetworkAdmin.App.Models;

public sealed record WorkbookUpdateResult(
    int UpdatedRows,
    int NotFoundRows,
    int SkippedRows,
    IReadOnlyList<string> NotFoundComputers);
