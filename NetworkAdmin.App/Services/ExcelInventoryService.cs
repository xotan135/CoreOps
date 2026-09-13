using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NetworkAdmin.App.Models;

namespace NetworkAdmin.App.Services;

public sealed class ExcelInventoryService
{
    private const int MaxHeaderRows = 25;
    private const int MaxColumns = 250;
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
        CancellationToken token) => Task.Run(() => Update(workbookPath, inventory, token), token);

    private static WorkbookUpdateResult Update(string workbookPath, IReadOnlyList<ComputerResult> inventory,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(workbookPath))
            throw new ArgumentException("Select an inventory workbook before collecting inventory.", nameof(workbookPath));
        if (!File.Exists(workbookPath))
            throw new FileNotFoundException("The inventory workbook was not found. Verify the path and your access to it.", workbookPath);
        if (!string.Equals(Path.GetExtension(workbookPath), ".xlsm", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select a macro-enabled Excel workbook (.xlsm).");

        var directory = Path.GetDirectoryName(Path.GetFullPath(workbookPath))!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(workbookPath)}.{Guid.NewGuid():N}.tmp");
        var backupPath = Path.Combine(directory, $".{Path.GetFileName(workbookPath)}.{Guid.NewGuid():N}.bak");

        try
        {
            File.Copy(workbookPath, temporaryPath, overwrite: false);
            WorkbookUpdateResult result;
            using (var document = SpreadsheetDocument.Open(temporaryPath, true))
            {
                if (document.DocumentType != SpreadsheetDocumentType.MacroEnabledWorkbook)
                    throw new InvalidDataException("The selected file is not a valid macro-enabled workbook.");
                var workbookPart = document.WorkbookPart
                    ?? throw new InvalidDataException("The workbook does not contain a workbook definition.");
                var locations = FindInventorySheets(workbookPart);
                var updated = 0;
                var notFound = 0;
                var skipped = 0;
                var notFoundComputers = new List<string>();

                foreach (var item in inventory)
                {
                    token.ThrowIfCancellationRequested();
                    if (!item.State.Equals("Online", StringComparison.OrdinalIgnoreCase))
                    {
                        skipped++;
                        continue;
                    }

                    var match = locations
                        .Select(location => (Location: location, Row: FindRow(location, item.ComputerName, workbookPart)))
                        .FirstOrDefault(candidate => candidate.Row is not null);
                    if (match.Row is null)
                    {
                        notFound++;
                        notFoundComputers.Add(item.ComputerName);
                        continue;
                    }

                    WriteMappedValues(match.Location.WorksheetPart, match.Row, match.Location.Columns, item);
                    updated++;
                }

                MarkForRecalculation(workbookPart);
                workbookPart.Workbook.Save();
                result = new WorkbookUpdateResult(updated, notFound, skipped, notFoundComputers);
            }

            token.ThrowIfCancellationRequested();
            File.Replace(temporaryPath, workbookPath, backupPath, ignoreMetadataErrors: true);
            File.Delete(backupPath);
            return result;
        }
        catch (IOException ex)
        {
            throw new IOException("CoreOps could not update the inventory workbook. Close it if another user has it open and verify write access to its folder.", ex);
        }
        finally
        {
            TryDelete(temporaryPath);
            TryDelete(backupPath);
        }
    }

    private static List<SheetLocation> FindInventorySheets(WorkbookPart workbookPart)
    {
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var results = new List<SheetLocation>();
        foreach (var sheetName in InventorySheets)
        {
            var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>()
                .FirstOrDefault(candidate => string.Equals(candidate.Name?.Value, sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet?.Id?.Value is null || workbookPart.GetPartById(sheet.Id.Value) is not WorksheetPart worksheetPart)
                continue;
            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            if (sheetData is null) continue;
            foreach (var row in sheetData.Elements<Row>().Where(row => row.RowIndex?.Value <= MaxHeaderRows))
            {
                var columns = MapColumns(row, sharedStrings);
                if (!columns.ContainsKey(nameof(ComputerResult.ComputerName))) continue;
                results.Add(new SheetLocation(worksheetPart, row.RowIndex!.Value, columns));
                break;
            }
        }

        if (results.Count == 0)
            throw new InvalidDataException("The Client Systems and Servers worksheets do not contain a recognized computer-name header.");
        return results;
    }

    private static Row? FindRow(SheetLocation location, string computerName, WorkbookPart workbookPart)
    {
        var keyColumn = location.Columns[nameof(ComputerResult.ComputerName)];
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var sheetData = location.WorksheetPart.Worksheet.GetFirstChild<SheetData>()!;
        foreach (var row in sheetData.Elements<Row>().Where(row => row.RowIndex?.Value > location.HeaderRow))
        {
            var cell = FindCell(row, keyColumn);
            if (cell is not null && computerName.Equals(GetCellText(cell, sharedStrings).Trim(), StringComparison.OrdinalIgnoreCase))
                return row;
        }
        return null;
    }

    private static Dictionary<string, uint> MapColumns(Row row, SharedStringTable? sharedStrings)
    {
        var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in row.Elements<Cell>())
        {
            var column = GetColumnIndex(cell.CellReference?.Value);
            if (column is 0 or > MaxColumns) continue;
            var header = GetCellText(cell, sharedStrings).Trim();
            if (header.Length == 0) continue;
            foreach (var pair in HeaderAliases)
                if (!result.ContainsKey(pair.Key) && pair.Value.Any(alias => alias.Equals(header, StringComparison.OrdinalIgnoreCase)))
                    result[pair.Key] = column;
        }
        return result;
    }

    private static void WriteMappedValues(WorksheetPart worksheetPart, Row row,
        IReadOnlyDictionary<string, uint> columns, ComputerResult item)
    {
        var values = new Dictionary<string, CellValueToWrite>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(item.ComputerName)] = CellValueToWrite.Text(item.ComputerName),
            [nameof(item.ServiceTag)] = CellValueToWrite.Text(item.ServiceTag),
            [nameof(item.LastUser)] = CellValueToWrite.Text(item.LastUser),
            [nameof(item.UserLastLogin)] = CellValueToWrite.Text(item.UserLastLogin),
            [nameof(item.Manufacturer)] = CellValueToWrite.Text(item.Manufacturer),
            [nameof(item.Model)] = CellValueToWrite.Text(item.Model),
            [nameof(item.Cpu)] = CellValueToWrite.Text(item.Cpu),
            [nameof(item.Ram)] = decimal.TryParse(item.Ram, NumberStyles.Number, CultureInfo.InvariantCulture, out var ram)
                ? CellValueToWrite.Number(ram.ToString(CultureInfo.InvariantCulture)) : CellValueToWrite.Text(item.Ram),
            [nameof(item.Architecture)] = CellValueToWrite.Text(item.Architecture),
            [nameof(item.BiosVersion)] = CellValueToWrite.Text(item.BiosVersion),
            [nameof(item.Edition)] = CellValueToWrite.Text(item.Edition),
            [nameof(item.Version)] = CellValueToWrite.Text(item.Version),
            [nameof(item.Build)] = CellValueToWrite.Text(item.Build),
            [nameof(item.Windows)] = CellValueToWrite.Text(item.Windows),
            [nameof(item.LastBoot)] = CellValueToWrite.Text(item.LastBoot),
            [nameof(item.IpAddress)] = CellValueToWrite.Text(item.IpAddress),
            [nameof(item.EthernetMac)] = CellValueToWrite.Text(item.EthernetMac),
            [nameof(item.WirelessMac)] = CellValueToWrite.Text(item.WirelessMac),
            [nameof(item.VncVersion)] = CellValueToWrite.Text(item.VncVersion),
            ["Updated"] = CellValueToWrite.Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
        };

        foreach (var pair in values)
        {
            if (!columns.TryGetValue(pair.Key, out var column)) continue;
            var cell = GetOrCreateCell(row, column);
            cell.CellFormula = null;
            if (pair.Value.IsNumber)
            {
                cell.DataType = CellValues.Number;
                cell.InlineString = null;
                cell.CellValue = new CellValue(pair.Value.Value);
            }
            else
            {
                cell.DataType = CellValues.InlineString;
                cell.CellValue = null;
                cell.InlineString = new InlineString(new Text(pair.Value.Value) { Space = SpaceProcessingModeValues.Preserve });
            }
        }
        worksheetPart.Worksheet.Save();
    }

    private static Cell GetOrCreateCell(Row row, uint column)
    {
        var existing = FindCell(row, column);
        if (existing is not null) return existing;
        var reference = $"{GetColumnName(column)}{row.RowIndex!.Value}";
        var cell = new Cell { CellReference = reference };
        var next = row.Elements<Cell>().FirstOrDefault(candidate => GetColumnIndex(candidate.CellReference?.Value) > column);
        if (next is null) row.Append(cell); else row.InsertBefore(cell, next);
        return cell;
    }

    private static Cell? FindCell(Row row, uint column) => row.Elements<Cell>()
        .FirstOrDefault(cell => GetColumnIndex(cell.CellReference?.Value) == column);

    private static string GetCellText(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(cell.CellValue?.Text, out var index))
            return sharedStrings?.Elements<SharedStringItem>().ElementAtOrDefault(index)?.InnerText ?? "";
        if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? "";
        return cell.CellValue?.Text ?? cell.InnerText ?? "";
    }

    private static uint GetColumnIndex(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return 0;
        uint result = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
            result = result * 26 + (uint)(char.ToUpperInvariant(character) - 'A' + 1);
        return result;
    }

    private static string GetColumnName(uint column)
    {
        var name = "";
        while (column > 0)
        {
            column--;
            name = (char)('A' + column % 26) + name;
            column /= 26;
        }
        return name;
    }

    private static void MarkForRecalculation(WorkbookPart workbookPart)
    {
        var properties = workbookPart.Workbook.CalculationProperties ??= new CalculationProperties();
        properties.SetAttribute(new OpenXmlAttribute("", "calcMode", "", "auto"));
        properties.SetAttribute(new OpenXmlAttribute("", "fullCalcOnLoad", "", "1"));
        properties.SetAttribute(new OpenXmlAttribute("", "forceFullCalc", "", "1"));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record SheetLocation(WorksheetPart WorksheetPart, uint HeaderRow, Dictionary<string, uint> Columns);
    private sealed record CellValueToWrite(string Value, bool IsNumber)
    {
        public static CellValueToWrite Text(string value) => new(value, false);
        public static CellValueToWrite Number(string value) => new(value, true);
    }
}
