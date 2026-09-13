namespace NetworkAdmin.App.Models;

public sealed class DomainComputer
{
    public string Name { get; init; } = "";
    public string DnsHostName { get; init; } = "";
    public string OperatingSystem { get; init; } = "";
    public string TargetName => string.IsNullOrWhiteSpace(DnsHostName) ? Name : DnsHostName;
    public string DisplayName => string.IsNullOrWhiteSpace(OperatingSystem)
        ? TargetName
        : $"{TargetName}  —  {OperatingSystem}";
}
