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
            try {
            $result = Invoke-Command -ComputerName '{{computerName}}' -Authentication Kerberos -ErrorAction Stop -ArgumentList '{{operation}}' -ScriptBlock {
                param($Operation)
                $identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
                $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
                $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
                if (-not $isAdmin) {
                    return [pscustomobject]@{ State='Remote not elevated'; Version=''; UpdateCount=$null; Updates=''; RebootRequired=$false; ExitCode=4; Message="The WinRM session for $identity does not have a local Administrator token on this computer. Verify local Administrators membership and remote UAC/GPO policy." }
                }
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
                $managementService = Get-Service -Name 'DellClientManagementService' -ErrorAction SilentlyContinue
                $serviceDescription = if ($managementService) { "Dell Client Management Service is $($managementService.Status)" } else { 'Dell Client Management Service was not found' }
                    if ($Operation -eq 'Scan') {
                        $arguments = @('/scan')
                    } else {
                        $arguments = @('/applyUpdates', '-forceupdate=enable', '-reboot=disable', '-autoSuspendBitLocker=enable')
                    }
                    try {
                        $dcuOutput = (& $cli @arguments 2>&1 | Out-String).Trim()
                        $code = $LASTEXITCODE
                    } catch {
                        return [pscustomobject]@{
                            State='DCU launch denied'; Version=$version; UpdateCount=$null; Updates='';
                            RebootRequired=$false; ExitCode=$null;
                            Message="Windows could not launch DCU as $identity from $cli. $($_.Exception.Message)"
                        }
                    }
                    if ($null -eq $code) {
                        return [pscustomobject]@{ State='DCU launch failed'; Version=$version; UpdateCount=$null; Updates=''; RebootRequired=$false; ExitCode=$null; Message="DCU did not provide an exit code. $dcuOutput" }
                    }
                    $count = $null
                    $updates = if ([string]::IsNullOrWhiteSpace($dcuOutput)) { 'DCU produced no console transcript.' } else { $dcuOutput }
                    if ($Operation -eq 'Scan') {
                        $countMatch = [regex]::Match($dcuOutput, 'Number of applicable updates[^:]*:\s*(\d+)', 'IgnoreCase')
                        if ($countMatch.Success) { $count = [int]$countMatch.Groups[1].Value }
                    }
                    $reboot = $code -in 1,5
                    if ($Operation -eq 'Scan') {
                        if ($code -eq 500) { $state='Up to date'; $count=0; $message='No applicable Dell updates were found.' }
                        elseif ($code -eq 0) { $state='Updates available'; $message=if ($count -gt 0) { "$count applicable update(s) found." } else { 'Scan completed; Dell reported applicable update information.' } }
                        elseif ($code -eq 4) { $state='DCU privilege rejected'; $message="DCU returned code 4 even though WinRM reports an Administrator token for $identity. $serviceDescription. Test dcu-cli.exe /scan from an elevated console on the target." }
                        elseif ($code -eq 5) { $state='Pending reboot'; $reboot=$true; $message='A reboot from a previous operation is pending.' }
                        elseif ($code -eq 6) { $state='DCU busy'; $message='Another Dell Command Update instance is running.' }
                        elseif ($code -eq 7) { $state='Unsupported model'; $message='Dell Command Update does not support this system model.' }
                        elseif ($code -eq 107) { $state='Invalid DCU option'; $message="DCU rejected an option value. $dcuOutput" }
                        elseif ($code -eq 503) { $state='Download failed'; $message='DCU could not download scan data. Check network and catalog access.' }
                        elseif ($code -eq 3000) { $state='DCU service stopped'; $message='Dell Client Management Service is not running.' }
                        elseif ($code -eq 3001) { $state='DCU service missing'; $message='Dell Client Management Service is not installed.' }
                        elseif ($code -eq 3002) { $state='DCU service disabled'; $message='Dell Client Management Service is disabled.' }
                        elseif ($code -in 3003,3004,3005) { $state='DCU service busy'; $message="Dell Client Management Service is busy (code $code). Retry after its current operation finishes." }
                        else { $state='Scan failed'; $message="Dell Command Update scan returned code $code." }
                    } else {
                        if ($code -eq 0) { $state='Installed'; $message='Applicable Dell updates were installed. No reboot was requested by DCU.' }
                        elseif ($code -eq 1) { $state='Reboot required'; $message='Updates installed; a reboot is required. CoreOps did not restart the computer.' }
                        elseif ($code -eq 500) { $state='Up to date'; $message='No applicable Dell updates were found.' }
                        elseif ($code -eq 4) { $state='DCU privilege rejected'; $message="DCU returned code 4 even though WinRM reports an Administrator token for $identity. $serviceDescription. Test dcu-cli.exe /applyUpdates from an elevated console on the target." }
                        elseif ($code -eq 5) { $state='Pending reboot'; $reboot=$true; $message='A reboot from a previous operation is pending.' }
                        elseif ($code -eq 6) { $state='DCU busy'; $message='Another Dell Command Update instance is running.' }
                        elseif ($code -eq 7) { $state='Unsupported model'; $message='Dell Command Update does not support this system model.' }
                        elseif ($code -eq 1002) { $state='Download failed'; $message='DCU could not download one or more updates. Check network and catalog access.' }
                        elseif ($code -eq 3000) { $state='DCU service stopped'; $message='Dell Client Management Service is not running.' }
                        elseif ($code -eq 3001) { $state='DCU service missing'; $message='Dell Client Management Service is not installed.' }
                        elseif ($code -eq 3002) { $state='DCU service disabled'; $message='Dell Client Management Service is disabled.' }
                        elseif ($code -in 3003,3004,3005) { $state='DCU service busy'; $message="Dell Client Management Service is busy (code $code). Retry after its current operation finishes." }
                        else { $state='Install failed'; $message="Dell Command Update installation returned code $code." }
                    }
                    [pscustomobject]@{ State=$state; Version=$version; UpdateCount=$count; Updates=$updates; RebootRequired=$reboot; ExitCode=$code; Message=$message }
            }
            } catch {
                $failureMessage = [string]$_.Exception.Message
                $failureId = [string]$_.FullyQualifiedErrorId
                $failureCategory = [string]$_.CategoryInfo.Category
                $failureState = if ($failureCategory -match 'PermissionDenied|SecurityError' -or $failureMessage -match 'denied|unauthorized') { 'WinRM access denied' } else { 'Remote command failed' }
                $result = [pscustomobject]@{
                    State=$failureState; Version=''; UpdateCount=$null; Updates=''; RebootRequired=$false; ExitCode=$null;
                    Message="$failureMessage [Category: $failureCategory; Error: $failureId]"
                }
            }
            $result | Select-Object State,Version,UpdateCount,Updates,RebootRequired,ExitCode,Message | ConvertTo-Json -Compress
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
        var lines = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var clean = lines.FirstOrDefault(line => !line.StartsWith("System.Management.Automation.", StringComparison.OrdinalIgnoreCase))
            ?? lines.FirstOrDefault() ?? "The remote command failed.";
        var state = Regex.IsMatch(clean, "access is denied|unauthorized|\\bdenied\\b", RegexOptions.IgnoreCase) ? "Access denied" :
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
            value = string.Join(Environment.NewLine, document.Descendants().Where(e => e.Name.LocalName == "S" && string.Equals((string?)e.Attribute("S"), "Error", StringComparison.OrdinalIgnoreCase)).Select(e => e.Value));
        }
        catch { value = Regex.Replace(value.Replace("#< CLIXML", "", StringComparison.OrdinalIgnoreCase), "<[^>]+>", " "); }
        return Regex.Replace(value, "_x([0-9A-Fa-f]{4})_", match => char.ConvertFromUtf32(Convert.ToInt32(match.Groups[1].Value, 16))).Trim();
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
