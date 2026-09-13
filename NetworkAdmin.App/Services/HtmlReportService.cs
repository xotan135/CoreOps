using System.Net;
using System.Text;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class HtmlReportService
{
    public string Create(string operation, IEnumerable<ComputerResult> results)
    {
        var rows = results.ToList();
        var groups = rows.GroupBy(result => result.State)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"<span class=\"metric\"><b>{group.Count()}</b> {Encode(group.Key)}</span>");
        var body = new StringBuilder();
        foreach (var result in rows)
        {
            var statusClass = result.State.Equals("Online", StringComparison.OrdinalIgnoreCase) ||
                              result.State.Equals("Restarting", StringComparison.OrdinalIgnoreCase)
                ? "good"
                : "problem";
            body.Append("<tr>")
                .Append($"<td>{Encode(result.ComputerName)}</td>")
                .Append($"<td><span class=\"status {statusClass}\">{Encode(result.State)}</span></td>")
                .Append($"<td>{Encode(result.Manufacturer)}</td>")
                .Append($"<td>{Encode(result.Model)}</td>")
                .Append($"<td>{Encode(result.Windows)}</td>")
                .Append($"<td>{Encode(result.IpAddress)}</td>")
                .Append($"<td>{Encode(result.LastBoot)}</td>")
                .Append($"<td>{Encode(result.Message)}</td>")
                .Append("</tr>");
        }

        var empty = rows.Count == 0 ? "<div class=\"empty\">No operations have run in this session.</div>" : "";
        return $$"""
            <!doctype html>
            <html><head><meta charset="utf-8"><meta http-equiv="X-UA-Compatible" content="IE=edge">
            <style>
            body{font-family:'Segoe UI',Arial,sans-serif;background:#151515;color:#ededed;margin:0;padding:26px}
            h1{font-size:26px;margin:0;color:#f4f4f4} .accent{color:#78ddd7}
            .subtitle{color:#aaa;margin:5px 0 20px}.summary{margin:0 0 18px}.metric{display:inline-block;background:#252525;border:1px solid #414141;border-radius:14px;padding:6px 11px;margin:0 7px 7px 0}
            table{width:100%;border-collapse:collapse;background:#1b1b1b;border:1px solid #3d3d3d}th{background:#303030;color:#fff;text-align:left;padding:9px;border-bottom:1px solid #484848}td{padding:8px 9px;border-bottom:1px solid #303030;vertical-align:top}tr:nth-child(even){background:#222}
            .status{white-space:nowrap;border-radius:10px;padding:3px 7px}.good{background:#244941;color:#bdf4df}.problem{background:#573638;color:#ffd6d8}.empty{padding:30px;background:#1b1b1b;border:1px solid #3d3d3d;color:#aaa}.foot{margin-top:16px;color:#888;font-size:12px}
            </style></head><body>
            <h1>CORE<span class="accent">OPS</span> session report</h1>
            <div class="subtitle">{{Encode(operation)}} · Updated {{DateTime.Now:yyyy-MM-dd HH:mm:ss}}</div>
            <div class="summary"><span class="metric"><b>{{rows.Count}}</b> total</span>{{string.Join("", groups)}}</div>
            {{empty}}
            {{(rows.Count == 0 ? "" : $"<table><thead><tr><th>Computer</th><th>State</th><th>Manufacturer</th><th>Model</th><th>Windows</th><th>IP address</th><th>Last boot</th><th>Message</th></tr></thead><tbody>{body}</tbody></table>")}}
            <div class="foot">This report exists only in the open CoreOps report window and is not saved to disk.</div>
            </body></html>
            """;
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
