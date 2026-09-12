using System.Text.Json;
using System.IO;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class AuditLogService
{
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkAdmin", "Logs");
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task WriteAsync(string operation, ComputerResult result, CancellationToken token)
    {
        Directory.CreateDirectory(_directory);
        var entry = JsonSerializer.Serialize(new
        {
            Timestamp = DateTimeOffset.Now, Operator = Environment.UserName, Operation = operation,
            result.ComputerName, result.State, result.Message
        });
        await _lock.WaitAsync(token);
        try { await File.AppendAllTextAsync(Path.Combine(_directory, $"audit-{DateTime.Today:yyyy-MM}.jsonl"), entry + Environment.NewLine, token); }
        finally { _lock.Release(); }
    }
}
