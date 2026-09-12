using System.Runtime.InteropServices;
using System.IO;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class ExcelInventoryService
{
    private const int MaxHeaderRows = 25;
    private static readonly string[] InventorySheets = ["Client Systems", "Servers"];

    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(ComputerResult.ComputerName)] = ["Name", "Computer", "Computer Name", "ComputerName", "Hostname", "Host Name"],
        [nameof(ComputerResult.ServiceTag)] = ["Service Tag", "ServiceTag", "Serial Number", "SerialNumber"],
        [nameof(ComputerResult.LastUser)] = ["Last User", "LastUser", "User", "Username"],
        [nameof(ComputerResult.UserLastLogin)] = ["User Last Login", "UserLastLogin", "Last Login"],
        [nameof(ComputerResult.Manufacturer)] = ["Manufacturer", "Make"],
        [nameof(ComputerResult.Model)] = ["Model"],
        [nameof(ComputerResult.Cpu)] = ["CPU", "Processor"],
        [nameof(ComputerResult.Ram)] = ["RAM", "Memory", "RAM (GB)", "Memory (GB)"],
        [nameof(ComputerResult.Architecture)] = ["Arch", "Architecture", "OS Architecture"],
        [nameof(ComputerResult.BiosVersion)] = ["BIOS Version", "BiosVersion", "BIOS"],
        [nameof(ComputerResult.Edition)] = ["Edition", "Windows Edition", "OS Edition"],
        [nameof(ComputerResult.Version)] = ["Version", "Windows Version", "OS Version"],
        [nameof(ComputerResult.Build)] = ["Build", "OS Build", "Build Number"],
        [nameof(ComputerResult.Windows)] = ["Windows", "Operating System", "OS"],
        [nameof(ComputerResult.LastBoot)] = ["Last Reboot", "LastReboot", "Last Boot", "LastBoot"],
        [nameof(ComputerResult.IpAddress)] = ["IP Address", "IPAddress", "IP"],
        [nameof(ComputerResult.EthernetMac)] = ["Ethernet MAC", "EthernetMac", "Wired MAC"],
        [nameof(ComputerResult.WirelessMac)] = ["Wireless MAC", "WirelessMac", "Wi-Fi MAC", "Wifi MAC"],
        [nameof(ComputerResult.VncVersion)] = ["VNC Version", "VNCVersion", "UltraVNC Version"],
        ["Updated"] = ["Updated", "Last Updated"],
    };

    public Task<WorkbookUpdateResult> UpdateAsync(string workbookPath, IReadOnlyList<ComputerResult> inventory,
        CancellationToken token)
    {
        var completion = new TaskCompletionSource<WorkbookUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Update(workbookPath, inventory, token)); }
            catch (OperationCanceledException) { completion.SetCanceled(token); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }

    private static WorkbookUpdateResult Update(string workbookPath, IReadOnlyList<ComputerResult> inventory,
        CancellationToken token)
    {
        if (!File.Exists(workbookPath))
            throw new FileNotFoundException("The inventory workbook was not found. Confirm that the G: drive is connected.", workbookPath);

        var excelType = Type.GetTypeFromProgID("Excel.Application")
            ?? throw new InvalidOperationException("Microsoft Excel is not installed on this computer.");
        dynamic? excel = null;
        dynamic? workbook = null;
        var locations = new List<SheetLocation>();

        try
        {
            excel = Activator.CreateInstance(excelType)!;
            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbook = excel.Workbooks.Open(workbookPath, UpdateLinks: 0, ReadOnly: false);
            if (workbook.ReadOnly)
                throw new IOException("The inventory workbook opened read-only. It may already be open or you may not have write permission.");

            locations = FindInventorySheets((object)workbook);
            var updated = 0; var notFound = 0; var skipped = 0;
            foreach (var item in inventory)
            {
                token.ThrowIfCancellationRequested();
                if (!item.State.Equals("Online", StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                SheetLocation? matchedLocation = null;
                var matchedRow = 0;
                foreach (var location in locations)
                {
                    matchedRow = FindRow(location, item.ComputerName);
                    if (matchedRow > 0) { matchedLocation = location; break; }
                }
                if (matchedLocation is null)
                {
                    notFound++;
                    continue;
                }
                WriteMappedValues(matchedLocation.Worksheet, matchedRow, matchedLocation.Columns, item);
                updated++;
            }

            workbook.Save();
            return new WorkbookUpdateResult(updated, notFound, skipped);
        }
        catch (COMException ex)
        {
            throw new IOException("Excel could not update the inventory workbook. Close the workbook if it is open and verify access to the shared drive.", ex);
        }
        finally
        {
            if (workbook is not null) { try { workbook.Close(SaveChanges: false); } catch { } }
            if (excel is not null) { try { excel.Quit(); } catch { } }
            foreach (var location in locations) ReleaseComObject(location.Worksheet);
            ReleaseComObject(workbook); ReleaseComObject(excel);
        }
    }

    private static List<SheetLocation> FindInventorySheets(object workbookObject)
    {
        dynamic workbook = workbookObject;
        var results = new List<SheetLocation>();
        foreach (var sheetName in InventorySheets)
        {
            dynamic? sheet = null;
            try
            {
                sheet = workbook.Worksheets[sheetName];
                var usedColumns = Math.Min((int)sheet.UsedRange.Columns.Count, 250);
                for (var row = 1; row <= MaxHeaderRows; row++)
                {
                    var columns = MapColumns(sheet, row, usedColumns);
                    if (!columns.ContainsKey(nameof(ComputerResult.ComputerName))) continue;
                    results.Add(new SheetLocation(sheet, row, columns));
                    sheet = null;
                    break;
                }
            }
            catch (COMException) { }
            finally { ReleaseComObject(sheet); }
        }
        if (results.Count == 0)
            throw new InvalidDataException("The Client Systems and Servers worksheets do not contain a recognized computer-name header.");
        return results;
    }

    private static int FindRow(SheetLocation location, string computerName)
    {
        var sheet = location.Worksheet;
        var keyColumn = location.Columns[nameof(ComputerResult.ComputerName)];
        var lastRow = Math.Max(location.HeaderRow, (int)sheet.Cells[sheet.Rows.Count, keyColumn].End(-4162).Row);
        for (var row = location.HeaderRow + 1; row <= lastRow; row++)
        {
            var key = Convert.ToString(sheet.Cells[row, keyColumn].Value2)?.Trim();
            if (computerName.Equals(key, StringComparison.OrdinalIgnoreCase)) return row;
        }
        return 0;
    }

    private static Dictionary<string, int> MapColumns(dynamic sheet, int headerRow, int columnCount)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= columnCount; column++)
        {
            var header = Convert.ToString(sheet.Cells[headerRow, column].Value2)?.Trim();
            if (string.IsNullOrWhiteSpace(header)) continue;
            foreach (var pair in HeaderAliases)
                if (!result.ContainsKey(pair.Key) && pair.Value.Any(alias => alias.Equals(header, StringComparison.OrdinalIgnoreCase)))
                    result[pair.Key] = column;
        }
        return result;
    }

    private static void WriteMappedValues(dynamic sheet, int row, IReadOnlyDictionary<string, int> columns, ComputerResult item)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(item.ComputerName)] = item.ComputerName, [nameof(item.ServiceTag)] = item.ServiceTag,
            [nameof(item.LastUser)] = item.LastUser, [nameof(item.UserLastLogin)] = item.UserLastLogin,
            [nameof(item.Manufacturer)] = item.Manufacturer, [nameof(item.Model)] = item.Model,
            [nameof(item.Cpu)] = item.Cpu, [nameof(item.Ram)] = decimal.TryParse(item.Ram, out var ram) ? ram : item.Ram,
            [nameof(item.Architecture)] = item.Architecture, [nameof(item.BiosVersion)] = item.BiosVersion,
            [nameof(item.Edition)] = item.Edition, [nameof(item.Version)] = item.Version, [nameof(item.Build)] = item.Build,
            [nameof(item.Windows)] = item.Windows, [nameof(item.LastBoot)] = item.LastBoot,
            [nameof(item.IpAddress)] = item.IpAddress, [nameof(item.EthernetMac)] = item.EthernetMac,
            [nameof(item.WirelessMac)] = item.WirelessMac, [nameof(item.VncVersion)] = item.VncVersion,
            ["Updated"] = DateTime.Now,
        };
        foreach (var pair in values)
            if (columns.TryGetValue(pair.Key, out var column)) sheet.Cells[row, column].Value2 = pair.Value;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }

    private sealed record SheetLocation(dynamic Worksheet, int HeaderRow, Dictionary<string, int> Columns);
}
