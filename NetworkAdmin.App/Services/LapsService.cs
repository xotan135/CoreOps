using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class LapsService
{
    public Task<IReadOnlyList<LapsComputer>> SearchAsync(string query, CancellationToken token)
    {
        var escapedQuery = EscapeLdap(query.Trim());
        var script = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " +
            "Import-Module ActiveDirectory -ErrorAction Stop; " +
            $"$items=@(Get-ADComputer -LDAPFilter '(|(name=*{escapedQuery}*)(dNSHostName=*{escapedQuery}*))' " +
            "-Properties DNSHostName,Enabled,LastLogonDate -ResultSetSize 100 | Sort-Object Name | " +
            "Select-Object Name,DNSHostName,DistinguishedName,@{n='EnabledDisplay';e={if($_.Enabled){'Enabled'}else{'Disabled'}}}," +
            "@{n='LastLogonDate';e={if($_.LastLogonDate){$_.LastLogonDate.ToString('o')}else{$null}}}); " +
            "ConvertTo-Json -InputObject $items -Compress";
        return RunJsonListAsync<LapsComputer>(script, token);
    }

    public async Task<IReadOnlyList<LapsPasswordResult>> GetPasswordsAsync(string computer, CancellationToken token)
    {
        ValidateComputerName(computer);
        var escapedComputer = EscapePowerShellLiteral(computer);
        var script = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " +
            "Import-Module LAPS -ErrorAction Stop; " +
            $"$all=@(Get-LapsADPassword -Identity '{escapedComputer}' -AsPlainText -IncludeHistory -ErrorAction Stop | " +
            "Sort-Object PasswordUpdateTime -Descending); " +
            "$items=@(for($i=0;$i -lt [Math]::Min(4,$all.Count);$i++){ $r=$all[$i]; [pscustomobject]@{" +
            "ComputerName=[string]$r.ComputerName;Account=[string]$r.Account;Password=[string]$r.Password;" +
            "ExpirationTimestamp=$(if($r.ExpirationTimestamp){$r.ExpirationTimestamp.ToString('o')}else{$null});" +
            "PasswordUpdateTime=$(if($r.PasswordUpdateTime){$r.PasswordUpdateTime.ToString('o')}else{$null});" +
            "IsHistorical=($i -gt 0)}}); ConvertTo-Json -InputObject $items -Compress";
        var results = await RunJsonListAsync<LapsPasswordResult>(script, token);
        return results.Count > 0
            ? results
            : throw new InvalidOperationException("No LAPS password was returned. Verify that the computer is managed and that your account can decrypt its password.");
    }

    public async Task RotateAsync(string computer, CancellationToken token)
    {
        ValidateComputerName(computer);
        var escapedComputer = EscapePowerShellLiteral(computer);
        var script = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " +
            "Import-Module LAPS -ErrorAction Stop; " +
            $"Set-LapsADPasswordExpirationTime -Identity '{escapedComputer}' -WhenEffective (Get-Date) -ErrorAction Stop | Out-Null; 'ok'";
        await RunAsync(script, token);
    }

    private static async Task<IReadOnlyList<T>> RunJsonListAsync<T>(string script, CancellationToken token)
    {
        var output = await RunAsync(script, token);
        if (string.IsNullOrWhiteSpace(output)) return [];
        using var document = JsonDocument.Parse(output);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<T>>(output, options) ?? []
            : [JsonSerializer.Deserialize<T>(output, options)!];
    }

    private static async Task<string> RunAsync(string script, CancellationToken token)
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
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows PowerShell could not be started.");
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

        var output = CleanPowerShellText(await outputTask);
        var error = CleanPowerShellText(await errorTask);
        if (process.ExitCode != 0) throw new InvalidOperationException(FriendlyError(error));
        return output;
    }

    private static string FriendlyError(string error)
    {
        if (Regex.IsMatch(error, "access is denied|unauthorized|insufficient access|decrypt", RegexOptions.IgnoreCase))
            return "Access denied. Your Windows account is not authorized to read or change this computer's LAPS password.";
        if (Regex.IsMatch(error, "Get-LapsADPassword|Set-LapsADPasswordExpirationTime|module.*LAPS|could not be loaded", RegexOptions.IgnoreCase))
            return "Microsoft Windows LAPS management tools are unavailable. Install the LAPS PowerShell module and try again.";
        if (Regex.IsMatch(error, "ActiveDirectory|Get-ADComputer", RegexOptions.IgnoreCase))
            return "The Active Directory PowerShell module is unavailable. Install RSAT Active Directory tools and try again.";
        return string.IsNullOrWhiteSpace(error) ? "The LAPS operation failed." : error;
    }

    private static string CleanPowerShellText(string value)
    {
        value = value.Trim();
        if (!value.Contains("#< CLIXML", StringComparison.OrdinalIgnoreCase)) return value;
        try
        {
            var document = XDocument.Parse(value[value.IndexOf('<')..]);
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
                char.ConvertFromUtf32(Convert.ToInt32(match.Groups[1].Value, 16))).Trim();
    }

    private static void ValidateComputerName(string value)
    {
        if (!Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$"))
            throw new ArgumentException("The selected computer name is invalid.", nameof(value));
    }

    private static string EscapePowerShellLiteral(string value) => value.Replace("'", "''");
    private static string EscapeLdap(string value) => value.Replace("\\", "\\5c").Replace("*", "\\2a")
        .Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00").Replace("'", "''");
}
