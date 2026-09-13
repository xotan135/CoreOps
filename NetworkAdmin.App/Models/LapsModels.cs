namespace NetworkAdmin.App.Models;

public sealed class LapsComputer
{
    public string Name { get; set; } = "";
    public string DnsHostName { get; set; } = "";
    public string DistinguishedName { get; set; } = "";
    public string EnabledDisplay { get; set; } = "";
    public DateTime? LastLogonDate { get; set; }
    public string LastLogonDisplay => LastLogonDate?.ToLocalTime().ToString("g") ?? "Not reported";
}

public sealed class LapsPasswordResult
{
    public string ComputerName { get; set; } = "";
    public string Account { get; set; } = "";
    public string Password { get; set; } = "";
    public DateTime? ExpirationTimestamp { get; set; }
    public DateTime? PasswordUpdateTime { get; set; }
    public bool IsHistorical { get; set; }
    public string Version => IsHistorical ? "Historical" : "Current";
    public string ChangedDisplay => PasswordUpdateTime?.ToLocalTime().ToString("g") ?? "Not reported";
    public string ExpiresDisplay => ExpirationTimestamp?.ToLocalTime().ToString("g") ?? "Not reported";
}

public sealed class LapsAuditEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string User { get; set; } = Environment.UserName;
    public string Action { get; set; } = "";
    public string Computer { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string Detail { get; set; } = "";
}
