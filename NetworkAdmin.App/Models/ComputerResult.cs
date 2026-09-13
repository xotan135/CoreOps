namespace NetworkAdmin.App.Models;

public sealed record class ComputerResult
{
    public string ComputerName { get; init; } = "";
    public string State { get; init; } = "Unknown";
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public string ServiceTag { get; init; } = "";
    public string LastUser { get; init; } = "";
    public string UserLastLogin { get; init; } = "";
    public string Cpu { get; init; } = "";
    public string Ram { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string BiosVersion { get; init; } = "";
    public string Edition { get; init; } = "";
    public string Version { get; init; } = "";
    public string Build { get; init; } = "";
    public string Windows { get; init; } = "";
    public string IpAddress { get; init; } = "";
    public string EthernetMac { get; init; } = "";
    public string WirelessMac { get; init; } = "";
    public string VncVersion { get; init; } = "";
    public string LastBoot { get; init; } = "";
    public string Message { get; init; } = "";
}
