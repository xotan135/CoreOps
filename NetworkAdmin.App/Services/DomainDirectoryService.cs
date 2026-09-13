using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class DomainDirectoryService
{
    public async Task<IReadOnlyList<DomainComputer>> GetComputersAsync(CancellationToken token)
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            Import-Module ActiveDirectory -ErrorAction Stop
            $computers = Get-ADComputer -Filter * -Properties DNSHostName, OperatingSystem, Enabled |
                Where-Object Enabled |
                Sort-Object Name |
                ForEach-Object {
                    [pscustomobject]@{
                        Name = [string]$_.Name
                        DnsHostName = [string]$_.DNSHostName
                        OperatingSystem = [string]$_.OperatingSystem
                    }
                }
            ConvertTo-Json -InputObject @($computers) -Compress
            """;

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -NoProfile -NonInteractive -EncodedCommand {encoded}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(token);
        var errorTask = process.StandardError.ReadToEndAsync(token);
        try
        {
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            throw;
        }

        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();
        if (process.ExitCode != 0)
        {
            var detail = error.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                ? "Active Directory could not be queried. Install the Active Directory PowerShell module and verify domain access."
                : detail);
        }

        return JsonSerializer.Deserialize<List<DomainComputer>>(output,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? [];
    }
}
