using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class DcuService
{
    public Task<DcuResult> ScanAsync(string computerName, CancellationToken token) =>
        ExecuteAsync(computerName, "Scan", token);

    public Task<DcuResult> InstallAsync(string computerName, CancellationToken token) =>
        ExecuteAsync(computerName, "Install", token);

    private static async Task<DcuResult> ExecuteAsync(string computerName, string operation, CancellationToken token)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $result = Invoke-Command -ComputerName '{{computerName}}' -ArgumentList '{{operation}}' -ScriptBlock {
                param($Operation)
                $manufacturer = [string](Get-CimInstance Win32_ComputerSystem).Manufacturer
                if ($manufacturer -notmatch 'Dell') {
                    return [pscustomobject]@{ State='Not a Dell'; Version=''; UpdateCount=$null; Updates=''; RebootRequired=$false; ExitCode=3; Message="Manufacturer is $manufacturer" }
                }

                $cli = @(
                    "$env:ProgramFiles\Dell\CommandUpdate\dcu-cli.exe",
                    "${env:ProgramFiles(x86)}\Dell\CommandUpdate\dcu-cli.exe"
                ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
                if (-not $cli) {
                    return [pscustomobject]@{ State='DCU missing'; Version=''; UpdateCount=$null; Updates=''; RebootRequired=$false; ExitCode=$null; Message='Dell Command Update is not installed.' }
                }

                $version = [string](Get-Item $cli).VersionInfo.ProductVersion
                $report = Join-Path $env:TEMP "CoreOps-DCU-$([guid]::NewGuid().ToString('N')).xml"
                try {
                    if ($Operation -eq 'Scan') {
                        $arguments = @('/scan', '-silent', "-report=$report")
                    } else {
                        $arguments = @('/applyUpdates', '-silent', '-reboot=disable', '-autoSuspendBitLocker=enable')
                    }
                    $process = Start-Process -FilePath $cli -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
                    $code = $process.ExitCode
                    $count = $null
                    $updates = ''
                    if ($Operation -eq 'Scan' -and (Test-Path $report)) {
                        try {
                            [xml]$xml = Get-Content -LiteralPath $report -Raw
                            $nodes = @($xml.SelectNodes("//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='update']"))
                            $count = $nodes.Count
                            $updates = ($nodes | ForEach-Object {
                                $name = $_.name
                                if (-not $name) { $name = $_.GetAttribute('name') }
                                if (-not $name) { $name = $_.title }
                                if ($name) { [string]$name }
                            } | Select-Object -Unique) -join '; '
                        } catch { }
                    }

                    $reboot = $code -in 1,5
                    if ($Operation -eq 'Scan') {
                        if ($code -eq 500) { $state='Up to date'; $count=0; $message='No applicable Dell updates were found.' }
                        elseif ($code -eq 0) { $state='Updates available'; $message=if ($count -gt 0) { "$count applicable update(s) found." } else { 'Scan completed; Dell reported applicable update information.' } }
                        elseif ($code -eq 4) { $state='Access denied'; $message='Dell Command Update requires administrative privileges on the remote computer.' }
                        elseif ($code -eq 5) { $state='Pending reboot'; $reboot=$true; $message='A reboot from a previous operation is pending.' }
                        elseif ($code -eq 6) { $state='DCU busy'; $message='Another Dell Command Update instance is running.' }
                        elseif ($code -eq 7) { $state='Unsupported model'; $message='Dell Command Update does not support this system model.' }
                        elseif ($code -eq 503) { $state='Download failed'; $message='DCU could not download scan data. Check network and catalog access.' }
                        else { $state='Scan failed'; $message="Dell Command Update scan returned code $code." }
                    } else {
                        if ($code -eq 0) { $state='Installed'; $message='Applicable Dell updates were installed. No reboot was requested by DCU.' }
                        elseif ($code -eq 1) { $state='Reboot required'; $message='Updates installed; a reboot is required. CoreOps did not restart the computer.' }
                        elseif ($code -eq 500) { $state='Up to date'; $message='No applicable Dell updates were found.' }
                        elseif ($code -eq 4) { $state='Access denied'; $message='Dell Command Update requires administrative privileges on the remote computer.' }
                        elseif ($code -eq 5) { $state='Pending reboot'; $reboot=$true; $message='A reboot from a previous operation is pending.' }
                        elseif ($code -eq 6) { $state='DCU busy'; $message='Another Dell Command Update instance is running.' }
                        elseif ($code -eq 7) { $state='Unsupported model'; $message='Dell Command Update does not support this system model.' }
                        elseif ($code -eq 1002) { $state='Download failed'; $message='DCU could not download one or more updates. Check network and catalog access.' }
                        else { $state='Install failed'; $message="Dell Command Update installation returned code $code." }
                    }
                    [pscustomobject]@{ State=$state; Version=$version; UpdateCount=$count; Updates=$updates; RebootRequired=$reboot; ExitCode=$code; Message=$message }
                } finally {
                    Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue
                }
            }
            $result | ConvertTo-Json -Compress
            """;

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -NoProfile -NonInteractive -OutputFormat Text -EncodedCommand {encoded}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(token);
        var errorTask = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw; }

        var output = CleanPowerShellText(await outputTask);
        var error = CleanPowerShellText(await errorTask);
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            return Failure(computerName, string.IsNullOrWhiteSpace(error) ? output : error);
        try
        {
            var value = JsonSerializer.Deserialize<DcuPayload>(output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new DcuResult { ComputerName=computerName, State=value?.State ?? "Command failed", Version=value?.Version ?? "", UpdateCount=value?.UpdateCount, Updates=value?.Updates ?? "", RebootRequired=value?.RebootRequired ?? false, ExitCode=value?.ExitCode, Message=value?.Message ?? "No result returned." };
        }
        catch (JsonException) { return Failure(computerName, string.IsNullOrWhiteSpace(error) ? output : error); }
    }

    private static DcuResult Failure(string computerName, string message)
    {
        var clean = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "The remote command failed.";
        var state = Regex.IsMatch(clean, "access is denied|unauthorized", RegexOptions.IgnoreCase) ? "Access denied" :
            Regex.IsMatch(clean, "WinRM|WS-Management|cannot connect|unreachable|timed out", RegexOptions.IgnoreCase) ? "WinRM unavailable" : "Command failed";
        return new DcuResult { ComputerName=computerName, State=state, Message=clean };
    }

    private static string CleanPowerShellText(string value)
    {
        value = value.Trim();
        if (!value.Contains("#< CLIXML", StringComparison.OrdinalIgnoreCase)) return value;
        try
        {
            var document = XDocument.Parse(value[value.IndexOf('<')..]);
            return string.Join(Environment.NewLine, document.Descendants().Where(e => e.Name.LocalName == "S" && string.Equals((string?)e.Attribute("S"), "Error", StringComparison.OrdinalIgnoreCase)).Select(e => e.Value));
        }
        catch { return Regex.Replace(value.Replace("#< CLIXML", "", StringComparison.OrdinalIgnoreCase), "<[^>]+>", " ").Trim(); }
    }

    private sealed class DcuPayload
    {
        public string? State { get; set; }
        public string? Version { get; set; }
        public int? UpdateCount { get; set; }
        public string? Updates { get; set; }
        public bool RebootRequired { get; set; }
        public int? ExitCode { get; set; }
        public string? Message { get; set; }
    }
}
