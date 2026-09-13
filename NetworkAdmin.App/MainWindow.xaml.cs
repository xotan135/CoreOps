using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetworkAdmin.App.Models;
using NetworkAdmin.App.Services;

namespace NetworkAdmin.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ComputerResult> _results = [];
    private readonly RemoteManagementService _remote = new();
    private readonly ExcelInventoryService _excelInventory = new();
    private readonly AuditLogService _audit = new();
    private readonly SettingsService _settings = new();
    private readonly HtmlReportService _htmlReport = new();
    private CancellationTokenSource? _cancellation;
    private SessionReportWindow? _reportWindow;
    private string _reportOperation = "Current session";

    public MainWindow()
    {
        InitializeComponent();
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        AppVersionTextBlock.Text = version is null ? "development" : $"v{version.Major}.{version.Minor}.{version.Build}";
        var settings = _settings.Load();
        InventoryPathTextBox.Text = settings.InventoryWorkbookPath;
        ProtectedNamesTextBox.Text = string.Join(Environment.NewLine, settings.ProtectedComputers);
        Closing += (_, _) => SaveSettings();
        ResultsGrid.ItemsSource = _results;
        AppendLog("Application ready. Commands run as the current Windows user.");
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("WinRM connectivity check", _remote.CheckWinRmAsync);

    private async void InventoryButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        await RunAsync("Inventory collection", _remote.GetInventoryAsync);
        var collected = _results.Where(result => result.State.Equals("Online", StringComparison.OrdinalIgnoreCase)).ToList();
        if (collected.Count == 0) return;

        var workbookPath = Environment.ExpandEnvironmentVariables(InventoryPathTextBox.Text.Trim());
        SetBusy(true, "Updating inventory workbook…");
        CancelButton.IsEnabled = false;
        try
        {
            var result = await _excelInventory.UpdateAsync(workbookPath, collected, CancellationToken.None);
            foreach (var computerName in result.NotFoundComputers)
            {
                var index = _results.ToList().FindIndex(item =>
                    item.ComputerName.Equals(computerName, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    var missingResult = _results[index] with
                    {
                        State = "Not in workbook",
                        Message = "Inventory was collected, but no matching row exists in Client Systems or Servers."
                    };
                    _results[index] = missingResult;
                    await _audit.WriteAsync("Inventory workbook update", missingResult, CancellationToken.None);
                    AppendLog($"{computerName}: Not in workbook — no matching row in Client Systems or Servers.");
                }
            }
            var message = $"Workbook saved. Updated: {result.UpdatedRows}; not found: {result.NotFoundRows}.";
            AppendLog(message);
            StatusTextBlock.Text = message;
            if (result.NotFoundRows > 0)
                MessageBox.Show($"{result.NotFoundRows} computer(s) were not found in Client Systems or Servers and were not added.",
                    "Inventory rows not found", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateReportWindow();
        }
        catch (Exception ex)
        {
            AppendLog($"Workbook update failed: {ex.Message}");
            StatusTextBlock.Text = "Workbook update failed";
            MessageBox.Show(ex.Message, "Could not update inventory workbook", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false, StatusTextBlock.Text); }
    }

    private void BrowseInventoryButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select inventory workbook",
            Filter = "Macro-enabled Excel workbook (*.xlsm)|*.xlsm",
            CheckFileExists = true,
            Multiselect = false
        };
        var currentPath = Environment.ExpandEnvironmentVariables(InventoryPathTextBox.Text.Trim());
        var currentDirectory = Path.GetDirectoryName(currentPath);
        if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            dialog.InitialDirectory = currentDirectory;
        if (dialog.ShowDialog(this) != true) return;
        InventoryPathTextBox.Text = dialog.FileName;
        SaveSettings();
    }

    private void OpenDomainPickerButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new DomainComputerPickerWindow { Owner = this };
        if (picker.ShowDialog() != true) return;
        var selected = picker.SelectedComputers.Select(computer => computer.TargetName);
        var targets = ParseNames(ComputerNamesTextBox.Text)
            .Concat(selected)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ComputerNamesTextBox.Text = string.Join(Environment.NewLine, targets);
        AppendLog($"Target list updated from Active Directory: {targets.Count} computer(s).");
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        var targets = ParseNames(ComputerNamesTextBox.Text);
        if (targets.Count == 0) { ShowTargetError(); return; }

        var protectedNames = ParseNames(ProtectedNamesTextBox.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowed = targets.Where(name => !protectedNames.Contains(name)).ToList();
        var blocked = targets.Where(protectedNames.Contains).ToList();
        if (blocked.Count > 0)
            AppendLog($"Protected; restart blocked: {string.Join(", ", blocked)}");
        if (allowed.Count == 0) return;

        var confirmation = MessageBox.Show(
            $"Restart {allowed.Count} computer(s) now?\n\n{string.Join(", ", allowed)}\n\nUnsaved user work may be lost.",
            "Confirm remote restart", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes) return;

        await RunAsync("Remote restart", _remote.RestartAsync, allowed);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();

    private void ReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_reportWindow is null)
        {
            _reportWindow = new SessionReportWindow { Owner = this };
            _reportWindow.Closed += (_, _) => _reportWindow = null;
            _reportWindow.Show();
        }
        else
        {
            _reportWindow.Activate();
        }
        UpdateReportWindow();
    }

    private void UpdateReportWindow()
    {
        _reportWindow?.ShowReport(_htmlReport.Create(_reportOperation, _results));
    }

    private async Task RunAsync(string operation, Func<string, CancellationToken, Task<ComputerResult>> action,
        IReadOnlyList<string>? suppliedTargets = null)
    {
        var targets = suppliedTargets ?? ParseNames(ComputerNamesTextBox.Text);
        if (targets.Count == 0) { ShowTargetError(); return; }

        SetBusy(true, $"{operation} running on {targets.Count} computer(s)…");
        _reportOperation = operation;
        _results.Clear();
        UpdateReportWindow();
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        var parallelism = int.Parse(((ComboBoxItem)ParallelismComboBox.SelectedItem).Content.ToString()!);
        using var gate = new SemaphoreSlim(parallelism);

        try
        {
            var jobs = targets.Select(async target =>
            {
                await gate.WaitAsync(token);
                try
                {
                    AppendLog($"{operation}: {target}");
                    var result = await action(target, token);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _results.Add(result);
                        UpdateReportWindow();
                    });
                    await _audit.WriteAsync(operation, result, token);
                    AppendLog($"{target}: {result.State} — {result.Message}");
                }
                catch (OperationCanceledException) { }
                finally { gate.Release(); }
            });
            await Task.WhenAll(jobs);
            StatusTextBlock.Text = token.IsCancellationRequested ? "Cancelled" : $"{operation} complete";
        }
        catch (OperationCanceledException) { StatusTextBlock.Text = "Cancelled"; }
        finally { _cancellation.Dispose(); _cancellation = null; SetBusy(false, StatusTextBlock.Text); }
    }

    private static List<string> ParseNames(string text) => text
        .Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(name => System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$"))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private void SetBusy(bool busy, string status)
    {
        CheckButton.IsEnabled = InventoryButton.IsEnabled = RestartButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        StatusTextBlock.Text = status;
    }

    private void AppendLog(string message) => Dispatcher.Invoke(() =>
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    });

    private static void ShowTargetError() => MessageBox.Show("Enter at least one valid computer name.", "Computer names required",
        MessageBoxButton.OK, MessageBoxImage.Information);

    private void SaveSettings() => _settings.Save(new AppSettings
    {
        InventoryWorkbookPath = InventoryPathTextBox.Text.Trim(),
        ProtectedComputers = ParseNames(ProtectedNamesTextBox.Text)
    });
}
