using System.Windows;
using System.Windows.Controls;
using NetworkAdmin.App.Models;
using NetworkAdmin.App.Services;

namespace NetworkAdmin.App;

public partial class DomainComputerPickerWindow : Window
{
    private readonly DomainDirectoryService _domainDirectory = new();
    private IReadOnlyList<DomainComputer> _domainComputers = [];

    public IReadOnlyList<DomainComputer> SelectedComputers { get; private set; } = [];

    public DomainComputerPickerWindow() => InitializeComponent();

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        RefreshButton.IsEnabled = false;
        AddButton.IsEnabled = false;
        StatusTextBlock.Text = "Loading enabled domain computers…";
        try
        {
            _domainComputers = await _domainDirectory.GetComputersAsync(CancellationToken.None);
            ApplyFilter();
            StatusTextBlock.Text = $"{_domainComputers.Count} enabled computer(s). Ctrl+click or Shift+click for multiple.";
        }
        catch (Exception ex)
        {
            _domainComputers = [];
            ComputerListBox.ItemsSource = null;
            StatusTextBlock.Text = "Could not load Active Directory computers.";
            MessageBox.Show(this, ex.Message, "Could not load domain computers", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            AddButton.IsEnabled = true;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchTextBox.Text.Trim();
        ComputerListBox.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _domainComputers
            : _domainComputers.Where(computer =>
                computer.TargetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                computer.OperatingSystem.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e) => ComputerListBox.SelectAll();

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedComputers = ComputerListBox.SelectedItems.Cast<DomainComputer>().ToList();
        if (SelectedComputers.Count == 0)
        {
            MessageBox.Show(this, "Select at least one domain computer.", "No computers selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
