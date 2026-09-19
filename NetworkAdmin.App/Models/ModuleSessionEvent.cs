namespace NetworkAdmin.App.Models;

public sealed record ModuleSessionEvent(DateTime Timestamp, string Action, string Computer, string Outcome, string Detail);
