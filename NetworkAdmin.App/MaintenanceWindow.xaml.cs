using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using NetworkAdmin.App.Models;
using NetworkAdmin.App.Services;

namespace NetworkAdmin.App;

public partial class MaintenanceWindow : Window
{
    private readonly RemoteManagementService _remote = new();
    private readonly AuditLogService _audit = new();
    private readonly ModuleReportService _reportService = new();
    private readonly SettingsService _settings = new();
    private readonly ObservableCollection<MaintenanceResult> _results = [];
    private CancellationTokenSource? _cancellation;
    private SessionReportWindow? _reportWindow;
    private string _reportOperation = "Remote maintenance";

    public MaintenanceWindow(IReadOnlyList<string> initialTargets)
    {
        InitializeComponent();
        WindowSizingService.RememberPlacement(this, "MaintenanceWindow");
        TargetsTextBox.Text = string.Join(Environment.NewLine, initialTargets);
        ResultsGrid.ItemsSource = _results;
        var settings = _settings.Load();
        if (settings.MaintenanceLogHeight > 0)
            MaintenanceLogRow.Height = new GridLength(Math.Clamp(settings.MaintenanceLogHeight, 50, 260));
        Closing += (_, _) => SaveLayout();
        AppendLog("Maintenance tools are ready. Commands run as the current Windows user.");
    }

    private async void QueryServiceButton_Click(object sender, RoutedEventArgs e) => await RunServiceAsync("Query");
    private async void StartServiceButton_Click(object sender, RoutedEventArgs e) => await RunServiceAsync("Start");
    private async void StopServiceButton_Click(object sender, RoutedEventArgs e) => await RunServiceAsync("Stop");
    private async void RestartServiceButton_Click(object sender, RoutedEventArgs e) => await RunServiceAsync("Restart");

    private async Task RunServiceAsync(string action)
    {
        var serviceName = ServiceNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            MessageBox.Show(this, "Enter the exact Windows service name, such as Spooler.", "Service name required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (action is "Stop" or "Restart")
        {
            var targets = ParseNames(TargetsTextBox.Text);
            if (targets.Count == 0) { ShowTargetError(); return; }
            var answer = MessageBox.Show(this, $"{action} service '{serviceName}' on {targets.Count} computer(s)?\n\n{string.Join(", ", targets)}",
                $"Confirm service {action.ToLowerInvariant()}", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }
        Func<string, CancellationToken, Task<ComputerResult>> operation = action == "Query"
            ? (computer, token) => _remote.GetServiceStatusAsync(computer, serviceName, token)
            : (computer, token) => _remote.ManageServiceAsync(computer, serviceName, action, token);
        await RunAsync($"Service {action.ToLowerInvariant()}: {serviceName}", serviceName, operation);
    }

    private async void PreviewCleanupButton_Click(object sender, RoutedEventArgs e)
    {
        var days = SelectedAgeDays();
        await RunAsync($"Temporary-file cleanup preview ({days}+ days)", "Windows and user temp files",
            (computer, token) => _remote.PreviewTempCleanupAsync(computer, days, token));
    }

    private async void RunCleanupButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = ParseNames(TargetsTextBox.Text);
        if (targets.Count == 0) { ShowTargetError(); return; }
        var days = SelectedAgeDays();
        var answer = MessageBox.Show(this,
            $"Delete files older than {days} days from Windows Temp and local user-profile Temp folders on {targets.Count} computer(s)?\n\n" +
            "Files that are in use or cannot be accessed will be skipped. This cannot be undone.",
            "Confirm temporary-file cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        await RunAsync($"Temporary-file cleanup ({days}+ days)", "Windows and user temp files",
            (computer, token) => _remote.CleanTempFilesAsync(computer, days, token), targets);
    }

    private async Task RunAsync(string operation, string item,
        Func<string, CancellationToken, Task<ComputerResult>> action, IReadOnlyList<string>? suppliedTargets = null)
    {
        var targets = suppliedTargets ?? ParseNames(TargetsTextBox.Text);
        if (targets.Count == 0) { ShowTargetError(); return; }
        _reportOperation = operation;
        _results.Clear();
        UpdateReportWindow();
        SetBusy(true, $"{operation} running on {targets.Count} computer(s)…");
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        using var gate = new SemaphoreSlim(4);
        try
        {
            var jobs = targets.Select(async computer =>
            {
                await gate.WaitAsync(token);
                try
                {
                    AppendLog($"{operation}: {computer}");
                    var result = await action(computer, token);
                    var maintenanceResult = new MaintenanceResult
                    {
                        ComputerName = computer, State = result.State, Item = item, Message = result.Message
                    };
                    await Dispatcher.InvokeAsync(() => { _results.Add(maintenanceResult); UpdateReportWindow(); });
                    await _audit.WriteAsync(operation, result, CancellationToken.None);
                    AppendLog($"{computer}: {result.State} — {result.Message}");
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

    private void SelectDomainButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new DomainComputerPickerWindow { Owner = this };
        if (picker.ShowDialog() != true) return;
        var targets = ParseNames(TargetsTextBox.Text).Concat(picker.SelectedComputers.Select(c => c.TargetName))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        TargetsTextBox.Text = string.Join(Environment.NewLine, targets);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();
    private void ReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_reportWindow is null) { _reportWindow = new SessionReportWindow { Owner = this, Title = "CoreOps — Maintenance session report" }; _reportWindow.Closed += (_, _) => _reportWindow = null; _reportWindow.Show(); }
        else _reportWindow.Activate();
        UpdateReportWindow();
    }
    private void UpdateReportWindow() => _reportWindow?.ShowReport(_reportService.CreateMaintenance(_reportOperation, _results));
    private int SelectedAgeDays() => int.Parse(((ComboBoxItem)CleanupAgeComboBox.SelectedItem).Tag.ToString()!);
    private void SetBusy(bool busy, string status)
    {
        QueryServiceButton.IsEnabled = StartServiceButton.IsEnabled = StopServiceButton.IsEnabled = RestartServiceButton.IsEnabled = PreviewCleanupButton.IsEnabled = RunCleanupButton.IsEnabled = TargetsTextBox.IsEnabled = !busy;
        CancelButton.IsEnabled = busy; ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed; StatusTextBlock.Text = status;
    }
    private void SaveLayout() { var settings = _settings.Load(); settings.MaintenanceLogHeight = MaintenanceLogRow.ActualHeight; _settings.Save(settings); }
    private void AppendLog(string message) => Dispatcher.Invoke(() => { LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); LogTextBox.ScrollToEnd(); });
    private static List<string> ParseNames(string text) => text.Split([',',';',' ','\t','\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(name => Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$")).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private void ShowTargetError() => MessageBox.Show(this, "Enter at least one valid computer name.", "Computer names required", MessageBoxButton.OK, MessageBoxImage.Information);
}
