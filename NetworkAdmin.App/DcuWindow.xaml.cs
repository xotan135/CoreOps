using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using NetworkAdmin.App.Models;
using NetworkAdmin.App.Services;

namespace NetworkAdmin.App;

public partial class DcuWindow : Window
{
    private readonly DcuService _dcu = new();
    private readonly AuditLogService _audit = new();
    private readonly ObservableCollection<DcuResult> _results = [];
    private CancellationTokenSource? _cancellation;
    private HashSet<string> _scannedInstallTargets = new(StringComparer.OrdinalIgnoreCase);

    public DcuWindow(IReadOnlyList<string> initialTargets)
    {
        InitializeComponent();
        WindowSizingService.RememberPlacement(this, "DcuWindow");
        TargetsTextBox.Text = string.Join(Environment.NewLine, initialTargets);
        ResultsGrid.ItemsSource = _results;
        AppendLog("Dell updates are scanned and installed through Dell Command Update on each remote computer.");
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync("Dell update scan", _dcu.ScanAsync);
        _scannedInstallTargets = _results.Where(result => result.State == "Updates available")
            .Select(result => result.ComputerName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = ParseNames(TargetsTextBox.Text);
        if (targets.Count == 0) { ShowTargetError(); return; }
        var installTargets = targets.Where(_scannedInstallTargets.Contains).ToList();
        if (installTargets.Count == 0)
        {
            MessageBox.Show(this, "Scan these computers first. Installation is enabled only for computers whose latest scan found applicable updates.",
                "Scan required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var confirmation = MessageBox.Show(this,
            $"Install all applicable Dell updates on {installTargets.Count} scanned computer(s)?\n\n{string.Join(", ", installTargets)}\n\n" +
            "This can update drivers, firmware, and BIOS. CoreOps will allow BitLocker suspension when Dell requires it, but will not reboot computers automatically. Scan first and ensure affected users have saved their work.",
            "Confirm Dell updates", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes) return;
        await RunAsync("Dell update installation", _dcu.InstallAsync, installTargets);
        _scannedInstallTargets.Clear();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();

    private void SelectDomainButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new DomainComputerPickerWindow { Owner = this };
        if (picker.ShowDialog() != true) return;
        var targets = ParseNames(TargetsTextBox.Text).Concat(picker.SelectedComputers.Select(c => c.TargetName)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        TargetsTextBox.Text = string.Join(Environment.NewLine, targets);
    }

    private async Task RunAsync(string operation, Func<string, CancellationToken, Task<DcuResult>> action, IReadOnlyList<string>? suppliedTargets = null)
    {
        var targets = suppliedTargets ?? ParseNames(TargetsTextBox.Text);
        if (targets.Count == 0) { ShowTargetError(); return; }
        SetBusy(true, $"{operation} running on {targets.Count} computer(s)…");
        _results.Clear();
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        using var gate = new SemaphoreSlim(4);
        try
        {
            var jobs = targets.Select(async target =>
            {
                await gate.WaitAsync(token);
                try
                {
                    AppendLog($"{operation}: {target}");
                    var result = await action(target, token);
                    await Dispatcher.InvokeAsync(() => _results.Add(result));
                    await _audit.WriteAsync(operation, new ComputerResult { ComputerName=result.ComputerName, State=result.State, Message=result.Message }, CancellationToken.None);
                    AppendLog($"{target}: {result.State} — {result.Message}");
                }
                catch (OperationCanceledException) { }
                finally { gate.Release(); }
            });
            await Task.WhenAll(jobs);
            StatusTextBlock.Text = token.IsCancellationRequested ? "Cancelled; a remote DCU process already started may finish." : $"{operation} complete";
        }
        catch (OperationCanceledException) { StatusTextBlock.Text = "Cancelled; a remote DCU process already started may finish."; }
        finally { _cancellation.Dispose(); _cancellation = null; SetBusy(false, StatusTextBlock.Text); }
    }

    private void SetBusy(bool busy, string status)
    {
        ScanButton.IsEnabled = InstallButton.IsEnabled = TargetsTextBox.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        StatusTextBlock.Text = status;
    }

    private void AppendLog(string message) => Dispatcher.Invoke(() => { LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); LogTextBox.ScrollToEnd(); });
    private static List<string> ParseNames(string text) => text.Split([',',';',' ','\t','\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(name => Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$")).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private void ShowTargetError() => MessageBox.Show(this, "Enter at least one valid computer name.", "Computer names required", MessageBoxButton.OK, MessageBoxImage.Information);
}
