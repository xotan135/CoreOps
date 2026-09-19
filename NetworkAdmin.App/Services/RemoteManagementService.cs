using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class RemoteManagementService
{
    public async Task<ComputerResult> CheckWinRmAsync(string computerName, CancellationToken token)
    {
        var script = $"$ErrorActionPreference='Stop'; Test-WSMan -ComputerName '{computerName}' | Out-Null; " +
                     "[pscustomobject]@{ State='Online'; Message='WinRM connection succeeded' } | ConvertTo-Json -Compress";
        return await ExecuteAsync(computerName, script, token);
    }

    public async Task<ComputerResult> GetInventoryAsync(string computerName, CancellationToken token)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $data = Invoke-Command -ComputerName '{{computerName}}' -ScriptBlock {
                $computer = Get-CimInstance Win32_ComputerSystem
                $os = Get-CimInstance Win32_OperatingSystem
                $bios = Get-CimInstance Win32_BIOS
                $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
                $windowsVersion = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
                $networks = Get-CimInstance Win32_NetworkAdapterConfiguration | Where-Object IPEnabled
                $network = $networks | Select-Object -First 1
                $ethernet = $networks | Where-Object { $_.Description -notmatch 'Wireless|Wi-Fi|WLAN|802\.11' } | Select-Object -First 1
                $wireless = $networks | Where-Object { $_.Description -match 'Wireless|Wi-Fi|WLAN|802\.11' } | Select-Object -First 1
                $lastProfile = Get-ChildItem 'C:\Users' -Directory -ErrorAction SilentlyContinue |
                    Where-Object Name -notin @('Public', 'Default', 'Default User', 'All Users') |
                    Sort-Object LastWriteTime -Descending | Select-Object -First 1
                $vncPath = @(
                    'C:\Program Files\uvnc bvba\UltraVNC\winvnc.exe',
                    'C:\Program Files\UltraVNC\winvnc.exe'
                ) | Where-Object { Test-Path $_ } | Select-Object -First 1
                $vncVersion = if ($vncPath) { (Get-Item $vncPath).VersionInfo.FileVersion } else { '' }
                [pscustomobject]@{
                    State = 'Online'
                    Manufacturer = [string]$computer.Manufacturer
                    Model = [string]$computer.Model
                    ServiceTag = [string]$bios.SerialNumber
                    LastUser = [string]$lastProfile.Name
                    UserLastLogin = if ($lastProfile) { $lastProfile.LastWriteTime.ToString('yyyy-MM-dd HH:mm') } else { '' }
                    Cpu = [string]$cpu.Name
                    Ram = [math]::Round($computer.TotalPhysicalMemory / 1GB, 2).ToString('0.##')
                    Architecture = [string]$os.OSArchitecture
                    BiosVersion = [string]$bios.SMBIOSBIOSVersion
                    Edition = [string]$os.Caption
                    Version = [string]$(if ($windowsVersion.DisplayVersion) { $windowsVersion.DisplayVersion } else { $windowsVersion.ReleaseId })
                    Build = "$($windowsVersion.CurrentBuild).$($windowsVersion.UBR)"
                    Windows = "$($os.Caption) ($($os.BuildNumber))"
                    IpAddress = [string]($network.IPAddress | Where-Object { $_ -match '^\d+\.' } | Select-Object -First 1)
                    EthernetMac = [string]$ethernet.MACAddress
                    WirelessMac = [string]$wireless.MACAddress
                    VncVersion = [string]$vncVersion
                    LastBoot = $os.LastBootUpTime.ToString('yyyy-MM-dd HH:mm')
                    Message = 'Inventory collected'
                }
            }
            $data | ConvertTo-Json -Compress
            """;
        return await ExecuteAsync(computerName, script, token);
    }

    public async Task<ComputerResult> RestartAsync(string computerName, CancellationToken token)
    {
        var script = $"$ErrorActionPreference='Stop'; Restart-Computer -ComputerName '{computerName}' -Force; " +
                     "[pscustomobject]@{ State='Restarting'; Message='Restart request accepted' } | ConvertTo-Json -Compress";
        return await ExecuteAsync(computerName, script, token);
    }

    public async Task<ComputerResult> GpUpdateAsync(string computerName, CancellationToken token)
    {
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $result = Invoke-Command -ComputerName '{{computerName}}' -ScriptBlock {
                $output = & gpupdate.exe /force 2>&1 | Out-String
                if ($LASTEXITCODE -ne 0) { throw $output.Trim() }
                $output.Trim()
            }
            [pscustomobject]@{
                State = 'Updated'
                Message = if ($result) { ($result | Out-String).Trim() } else { 'Group Policy refresh completed' }
            } | ConvertTo-Json -Compress
            """;
        return await ExecuteAsync(computerName, script, token);
    }

    public Task<ComputerResult> GetServiceStatusAsync(string computerName, string serviceName, CancellationToken token) =>
        ManageServiceAsync(computerName, serviceName, "Query", token);

    public async Task<ComputerResult> ManageServiceAsync(string computerName, string serviceName, string action,
        CancellationToken token)
    {
        var escapedService = serviceName.Replace("'", "''", StringComparison.Ordinal);
        var escapedAction = action.Replace("'", "''", StringComparison.Ordinal);
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $data = Invoke-Command -ComputerName '{{computerName}}' -ScriptBlock {
                param($serviceName, $action)
                $service = Get-Service -Name $serviceName -ErrorAction Stop
                switch ($action) {
                    'Start'   { Start-Service -InputObject $service -ErrorAction Stop; $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30)) }
                    'Stop'    { Stop-Service -InputObject $service -ErrorAction Stop; $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30)) }
                    'Restart' { Restart-Service -InputObject $service -ErrorAction Stop; $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(45)) }
                }
                $service.Refresh()
                $configuration = Get-CimInstance Win32_Service -Filter "Name='$($service.Name.Replace("'", "''"))'" -ErrorAction SilentlyContinue
                [pscustomobject]@{
                    State = [string]$service.Status
                    Message = "$($service.DisplayName) ($($service.Name)); startup: $($configuration.StartMode)"
                }
            } -ArgumentList '{{escapedService}}', '{{escapedAction}}'
            $data | ConvertTo-Json -Compress
            """;
        return await ExecuteAsync(computerName, script, token);
    }

    public async Task<ComputerResult> PreviewTempCleanupAsync(string computerName, int minimumAgeDays,
        CancellationToken token) => await RunTempCleanupAsync(computerName, minimumAgeDays, false, token);

    public async Task<ComputerResult> CleanTempFilesAsync(string computerName, int minimumAgeDays,
        CancellationToken token) => await RunTempCleanupAsync(computerName, minimumAgeDays, true, token);

    private async Task<ComputerResult> RunTempCleanupAsync(string computerName, int minimumAgeDays, bool delete,
        CancellationToken token)
    {
        var safeDays = Math.Clamp(minimumAgeDays, 1, 365);
        var deleteLiteral = delete ? "$true" : "$false";
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $data = Invoke-Command -ComputerName '{{computerName}}' -ScriptBlock {
                param($minimumAgeDays, $delete)
                $cutoff = (Get-Date).AddDays(-[int]$minimumAgeDays)
                $roots = [System.Collections.Generic.List[string]]::new()
                $windowsTemp = Join-Path $env:windir 'Temp'
                if (Test-Path -LiteralPath $windowsTemp -PathType Container) { $roots.Add($windowsTemp) }
                Get-ChildItem -LiteralPath (Join-Path $env:SystemDrive 'Users') -Directory -Force -ErrorAction SilentlyContinue |
                    Where-Object { -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) } |
                    ForEach-Object {
                        $userTemp = Join-Path $_.FullName 'AppData\Local\Temp'
                        if (Test-Path -LiteralPath $userTemp -PathType Container) { $roots.Add($userTemp) }
                    }

                $files = foreach ($root in $roots | Select-Object -Unique) {
                    Get-ChildItem -LiteralPath $root -File -Recurse -Force -ErrorAction SilentlyContinue |
                        Where-Object { $_.LastWriteTime -lt $cutoff -and -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) }
                }
                $files = @($files)
                $bytes = [long](($files | Measure-Object Length -Sum).Sum)
                if (-not $delete) {
                    [pscustomobject]@{
                        State = 'Preview'
                        Message = "Eligible: $($files.Count) file(s), $([math]::Round($bytes / 1MB, 2)) MB in $($roots.Count) temp location(s); older than $minimumAgeDays day(s)"
                    }
                    return
                }

                $deleted = 0; $failed = 0; $freed = [long]0
                foreach ($file in $files) {
                    try { Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop; $deleted++; $freed += $file.Length }
                    catch { $failed++ }
                }
                foreach ($root in $roots | Select-Object -Unique) {
                    Get-ChildItem -LiteralPath $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
                        Sort-Object FullName -Descending | ForEach-Object {
                            if (-not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -and
                                -not (Get-ChildItem -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue | Select-Object -First 1)) {
                                Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
                            }
                        }
                }
                [pscustomobject]@{
                    State = if ($failed -eq 0) { 'Cleaned' } else { 'Completed with skips' }
                    Message = "Deleted: $deleted file(s), $([math]::Round($freed / 1MB, 2)) MB; failed/in use: $failed"
                }
            } -ArgumentList {{safeDays}}, {{deleteLiteral}}
            $data | ConvertTo-Json -Compress
            """;
        return await ExecuteAsync(computerName, script, token);
    }

    private static async Task<ComputerResult> ExecuteAsync(string computerName, string script, CancellationToken token)
    {
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
            return Failed(computerName, CleanError(error, output));

        try
        {
            var payload = JsonSerializer.Deserialize<PowerShellResult>(output,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new ComputerResult
            {
                ComputerName = computerName, State = payload?.State ?? "Online", Manufacturer = payload?.Manufacturer ?? "",
                Model = payload?.Model ?? "", ServiceTag = payload?.ServiceTag ?? "", LastUser = payload?.LastUser ?? "",
                UserLastLogin = payload?.UserLastLogin ?? "", Cpu = payload?.Cpu ?? "", Ram = payload?.Ram ?? "",
                Architecture = payload?.Architecture ?? "", BiosVersion = payload?.BiosVersion ?? "",
                Edition = payload?.Edition ?? "", Version = payload?.Version ?? "", Build = payload?.Build ?? "",
                Windows = payload?.Windows ?? "", IpAddress = payload?.IpAddress ?? "", EthernetMac = payload?.EthernetMac ?? "",
                WirelessMac = payload?.WirelessMac ?? "", VncVersion = payload?.VncVersion ?? "",
                LastBoot = payload?.LastBoot ?? "", Message = payload?.Message ?? "Completed"
            };
        }
        catch (JsonException) { return Failed(computerName, CleanError(error, output)); }
    }

    private static ComputerResult Failed(string computerName, string message)
    {
        var category = CategorizeFailure(computerName, message);
        return new ComputerResult { ComputerName = computerName, State = category, Message = message };
    }

    private static string CleanError(string error, string output)
    {
        var value = string.IsNullOrWhiteSpace(error) ? output : error;
        var line = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(line) ? "The command failed without an error message." : line.Trim();
    }

    private static string CategorizeFailure(string computerName, string message)
    {
        if (Regex.IsMatch(message, "cannot find any service with service name|no service was found", RegexOptions.IgnoreCase))
            return "Service not found";
        if (Regex.IsMatch(message, "access is denied|unauthorized|0x80070005|authentication failed", RegexOptions.IgnoreCase))
            return "Access denied";
        try { _ = Dns.GetHostAddresses(computerName); }
        catch (SocketException) { return "Not found"; }
        if (Regex.IsMatch(message, "no such host|name.*(cannot be resolved|does not exist)|cannot find the computer|dns", RegexOptions.IgnoreCase))
            return "Not found";
        if (Regex.IsMatch(message, "timed out|unreachable|network path was not found|rpc server is unavailable|0x800706ba|winrm|ws-management|client cannot connect to the destination", RegexOptions.IgnoreCase))
        {
            try
            {
                using var ping = new Ping();
                return ping.Send(computerName, 1200)?.Status == IPStatus.Success
                    ? "WinRM unavailable"
                    : "Offline/unreachable";
            }
            catch (PingException) { return "Offline/unreachable"; }
        }
        return "Command failed";
    }

    private static string CleanPowerShellText(string value)
    {
        value = value.Trim();
        if (!value.Contains("#< CLIXML", StringComparison.OrdinalIgnoreCase)) return value;
        try
        {
            var xmlStart = value.IndexOf('<');
            var document = XDocument.Parse(value[xmlStart..]);
            value = string.Join(Environment.NewLine, document.Descendants()
                .Where(element => element.Name.LocalName == "S" &&
                                  string.Equals((string?)element.Attribute("S"), "Error", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Value));
        }
        catch
        {
            value = Regex.Replace(value.Replace("#< CLIXML", "", StringComparison.OrdinalIgnoreCase), "<[^>]+>", " ");
        }

        return Regex.Replace(value, "_x([0-9A-Fa-f]{4})_", match =>
                char.ConvertFromUtf32(Convert.ToInt32(match.Groups[1].Value, 16)))
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
    }

    private sealed class PowerShellResult
    {
        public string? State { get; set; } public string? Manufacturer { get; set; } public string? Model { get; set; }
        public string? ServiceTag { get; set; } public string? LastUser { get; set; } public string? UserLastLogin { get; set; }
        public string? Cpu { get; set; } public string? Ram { get; set; } public string? Architecture { get; set; }
        public string? BiosVersion { get; set; } public string? Edition { get; set; } public string? Version { get; set; }
        public string? Build { get; set; } public string? Windows { get; set; } public string? IpAddress { get; set; }
        public string? EthernetMac { get; set; } public string? WirelessMac { get; set; } public string? VncVersion { get; set; }
        public string? LastBoot { get; set; }
        public string? Message { get; set; }
    }
}
