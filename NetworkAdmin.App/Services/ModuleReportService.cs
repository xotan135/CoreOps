using System.Net;
using System.Text;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class ModuleReportService
{
    public string CreateDcu(string operation, IEnumerable<DcuResult> source)
    {
        var rows = source.ToList();
        var body = new StringBuilder();
        foreach (var item in rows)
            body.Append("<tr>").Append(Cell(item.ComputerName)).Append(Cell(item.State)).Append(Cell(item.Version))
                .Append(Cell(item.UpdateCountDisplay)).Append(Cell(item.RebootDisplay)).Append(Cell(item.Message))
                .Append($"<td><pre>{Encode(item.Updates)}</pre></td></tr>");
        return Page("Dell Updates", operation, rows.Count,
            "<th>Computer</th><th>State</th><th>DCU version</th><th>Count</th><th>Reboot</th><th>Message</th><th>Transcript</th>", body.ToString(),
            "This report and the captured DCU transcript exist only in memory while the report window is open.");
    }

    public string CreateLaps(IEnumerable<ModuleSessionEvent> source)
    {
        var rows = source.ToList();
        var body = new StringBuilder();
        foreach (var item in rows)
            body.Append("<tr>").Append(Cell(item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))).Append(Cell(item.Action))
                .Append(Cell(item.Computer)).Append(Cell(item.Outcome)).Append(Cell(item.Detail)).Append("</tr>");
        return Page("Windows LAPS", "Password-free session activity", rows.Count,
            "<th>Time</th><th>Action</th><th>Computer</th><th>Outcome</th><th>Detail</th>", body.ToString(),
            "This report exists only in memory while the report window is open. LAPS passwords are never included.");
    }

    private static string Page(string module, string subtitle, int count, string headers, string body, string footer) => $$"""
        <!doctype html><html><head><meta charset="utf-8"><meta http-equiv="X-UA-Compatible" content="IE=edge"><style>
        body{font-family:'Segoe UI',Arial,sans-serif;background:#151515;color:#ededed;margin:0;padding:24px}h1{margin:0}.accent{color:#78ddd7}.sub{color:#aaa;margin:5px 0 18px}.metric{display:inline-block;background:#252525;border:1px solid #414141;border-radius:14px;padding:6px 11px;margin-bottom:15px}table{width:100%;border-collapse:collapse;background:#1b1b1b;border:1px solid #3d3d3d}th{background:#303030;text-align:left;padding:9px}td{padding:8px;border-bottom:1px solid #303030;vertical-align:top}tr:nth-child(even){background:#222}pre{white-space:pre-wrap;margin:0;font-family:Consolas,monospace}.empty{padding:25px;border:1px solid #3d3d3d;color:#aaa}.foot{margin-top:14px;color:#888;font-size:12px}
        </style></head><body><h1>CORE<span class="accent">OPS</span> {{Encode(module)}}</h1><div class="sub">{{Encode(subtitle)}} · Updated {{DateTime.Now:yyyy-MM-dd HH:mm:ss}}</div><span class="metric"><b>{{count}}</b> session result(s)</span>{{(count == 0 ? "<div class=\"empty\">No operations have run in this session.</div>" : $"<table><thead><tr>{headers}</tr></thead><tbody>{body}</tbody></table>")}}<div class="foot">{{Encode(footer)}}</div></body></html>
        """;

    private static string Cell(string? value) => $"<td>{Encode(value)}</td>";
    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
