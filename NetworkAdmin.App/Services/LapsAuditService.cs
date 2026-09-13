using System.IO;
using System.Text.Json;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class LapsAuditService
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoreOps", "Logs", "laps-audit.jsonl");

    public IReadOnlyList<LapsAuditEntry> Read()
    {
        try
        {
            return File.Exists(_path)
                ? File.ReadLines(_path).Select(line => JsonSerializer.Deserialize<LapsAuditEntry>(line))
                    .Where(entry => entry is not null).Cast<LapsAuditEntry>()
                    .OrderByDescending(entry => entry.Timestamp).Take(500).ToList()
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public void Write(string action, string computer, string outcome, string detail = "")
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var entry = new LapsAuditEntry
            {
                Action = action,
                Computer = computer,
                Outcome = outcome,
                Detail = detail.Length > 500 ? detail[..500] : detail
            };
            File.AppendAllText(_path, JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A local convenience audit failure must not expose or interrupt a credential operation.
        }
    }
}
