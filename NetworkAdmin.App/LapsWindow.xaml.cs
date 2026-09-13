using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NetworkAdmin.App.Models;
using NetworkAdmin.App.Services;

namespace NetworkAdmin.App;

public partial class LapsWindow : Window
{
    private readonly LapsService _laps = new();
    private readonly LapsAuditService _audit = new();
    private readonly ObservableCollection<LapsComputer> _computers = [];
    private readonly ObservableCollection<LapsPasswordResult> _passwords = [];
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _clipboardTimer;
    private string _selectedPassword = "";
    private string _copiedValue = "";

    public LapsWindow()
    {
        InitializeComponent();
        ComputerGrid.ItemsSource = _computers;
        PasswordHistoryGrid.ItemsSource = _passwords;
        LoadAudit();
    }

    private LapsComputer? SelectedComputer => ComputerGrid.SelectedItem as LapsComputer;

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchTextBox.Text.Trim();
        if (query.Length < 2)
        {
            MessageBox.Show(this, "Enter at least two characters.", "Search text required",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ClearSecret();
        SetBusy(true, "Searching Active Directory…");
        try
        {
            var results = await _laps.SearchAsync(query, NewOperationToken());
            _computers.Clear();
            foreach (var item in results) _computers.Add(item);
            ComputerGrid.SelectedItem = results.FirstOrDefault();
            StatusTextBlock.Text = results.Count == 0 ? "No computers found." : $"{results.Count} computer(s) found.";
            _audit.Write("Search", "", "Success", $"Query returned {results.Count} result(s)");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Fail("Search", "", ex);
        }
        finally
        {
            SetBusy(false, StatusTextBlock.Text);
            LoadAudit();
        }
    }

    private void ComputerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearSecret();
        var computer = SelectedComputer;
        SelectedComputerTextBlock.Text = computer?.Name ?? "No computer selected";
        SelectedDnsTextBlock.Text = computer?.DnsHostName ?? "";
        RetrieveButton.IsEnabled = RotateButton.IsEnabled = computer is not null;
    }

    private async void RetrieveButton_Click(object sender, RoutedEventArgs e)
    {
        var computer = SelectedComputer;
        if (computer is null) return;
        ClearSecret();
        SetBusy(true, $"Retrieving password for {computer.Name}…");
        try
        {
            var results = await _laps.GetPasswordsAsync(computer.Name, NewOperationToken());
            foreach (var item in results) _passwords.Add(item);
            PasswordHistoryGrid.SelectedItem = results.FirstOrDefault();
            StatusTextBlock.Text = $"Current password and {Math.Max(0, results.Count - 1)} historical password(s) retrieved.";
            _audit.Write("Retrieve password history", computer.Name, "Success",
                $"Returned current plus {Math.Max(0, results.Count - 1)} historical entries");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Fail("Retrieve password", computer.Name, ex);
        }
        finally
        {
            SetBusy(false, StatusTextBlock.Text);
            LoadAudit();
        }
    }

    private void PasswordHistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPassword = (PasswordHistoryGrid.SelectedItem as LapsPasswordResult)?.Password ?? "";
        CopyPasswordButton.IsEnabled = _selectedPassword.Length > 0;
        UpdatePasswordDisplay();
    }

    private void RevealPasswordCheckBox_Click(object sender, RoutedEventArgs e) => UpdatePasswordDisplay();

    private void CopyPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPassword.Length == 0) return;
        Clipboard.SetText(_selectedPassword);
        _copiedValue = _selectedPassword;
        StatusTextBlock.Text = "Copied. The clipboard will clear in 30 seconds.";
        _audit.Write("Copy password", SelectedComputer?.Name ?? "", "Success", "Clipboard timeout: 30 seconds");
        LoadAudit();
        StartClipboardTimer(_copiedValue, SelectedComputer?.Name ?? "");
    }

    private void StartClipboardTimer(string copiedValue, string computer)
    {
        _clipboardTimer?.Cancel();
        _clipboardTimer?.Dispose();
        _clipboardTimer = new CancellationTokenSource();
        var token = _clipboardTimer.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                await Dispatcher.InvokeAsync(() => ClearClipboardIfUnchanged(copiedValue, computer));
            }
            catch (OperationCanceledException) { }
        });
    }

    private void ClearClipboardIfUnchanged(string copiedValue, string computer)
    {
        if (Clipboard.ContainsText() && Clipboard.GetText() == copiedValue)
        {
            Clipboard.Clear();
            _copiedValue = "";
            StatusTextBlock.Text = "Clipboard cleared.";
            _audit.Write("Clear clipboard", computer, "Success", "Automatic 30-second timeout");
            LoadAudit();
        }
    }

    private async void RotateButton_Click(object sender, RoutedEventArgs e)
    {
        var computer = SelectedComputer;
        if (computer is null) return;
        var answer = MessageBox.Show(this,
            $"Expire the LAPS password for {computer.Name} now?\n\nThe device rotates it after policy processing and contact with Active Directory.",
            "Request LAPS password rotation", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        ClearSecret();
        SetBusy(true, $"Requesting password rotation for {computer.Name}…");
        try
        {
            await _laps.RotateAsync(computer.Name, NewOperationToken());
            StatusTextBlock.Text = "Rotation requested. The new password is not immediate.";
            _audit.Write("Request password rotation", computer.Name, "Success");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Fail("Request password rotation", computer.Name, ex);
        }
        finally
        {
            SetBusy(false, StatusTextBlock.Text);
            LoadAudit();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSecret();
        StatusTextBlock.Text = "Password cleared from the window.";
    }

    private void RefreshAuditButton_Click(object sender, RoutedEventArgs e) => LoadAudit();

    private CancellationToken NewOperationToken()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        return _operationCancellation.Token;
    }

    private void SetBusy(bool busy, string status)
    {
        SearchTextBox.IsEnabled = !busy;
        ComputerGrid.IsEnabled = !busy;
        RetrieveButton.IsEnabled = !busy && SelectedComputer is not null;
        RotateButton.IsEnabled = !busy && SelectedComputer is not null;
        StatusTextBlock.Text = status;
    }

    private void UpdatePasswordDisplay()
    {
        PasswordDisplayTextBox.Text = _selectedPassword.Length == 0
            ? ""
            : RevealPasswordCheckBox.IsChecked == true ? _selectedPassword : "••••••••••••";
    }

    private void ClearSecret()
    {
        _passwords.Clear();
        PasswordHistoryGrid.SelectedItem = null;
        _selectedPassword = "";
        RevealPasswordCheckBox.IsChecked = false;
        CopyPasswordButton.IsEnabled = false;
        PasswordDisplayTextBox.Text = "";
    }

    private void Fail(string action, string computer, Exception exception)
    {
        StatusTextBlock.Text = exception.Message;
        var detail = exception.Message.Length > 500 ? exception.Message[..500] : exception.Message;
        _audit.Write(action, computer, "Failed", detail);
    }

    private void LoadAudit() => AuditGrid.ItemsSource = _audit.Read();

    private void Window_Closed(object? sender, EventArgs e)
    {
        _operationCancellation?.Cancel();
        _clipboardTimer?.Cancel();
        if (_copiedValue.Length > 0) ClearClipboardIfUnchanged(_copiedValue, SelectedComputer?.Name ?? "");
        ClearSecret();
    }
}
