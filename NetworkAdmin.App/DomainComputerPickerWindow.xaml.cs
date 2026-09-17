using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NetworkAdmin.App.Models;
using NetworkAdmin.App.Services;

namespace NetworkAdmin.App;

public partial class DomainComputerPickerWindow : Window
{
    private readonly DomainDirectoryService _domainDirectory = new();
    private IReadOnlyList<DomainComputer> _domainComputers = [];
    private ICollectionView? _computerView;

    public IReadOnlyList<DomainComputer> SelectedComputers { get; private set; } = [];

    public DomainComputerPickerWindow()
    {
        InitializeComponent();
        WindowSizingService.RememberPlacement(this, "DomainComputerPickerWindow");
    }

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
            PopulateOperatingSystemFilter();
            ComputerGrid.ItemsSource = _domainComputers;
            _computerView = CollectionViewSource.GetDefaultView(ComputerGrid.ItemsSource);
            _computerView.Filter = MatchesFilter;
            _computerView.SortDescriptions.Clear();
            _computerView.SortDescriptions.Add(new SortDescription(nameof(DomainComputer.TargetName), ListSortDirection.Ascending));
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _domainComputers = [];
            _computerView = null;
            ComputerGrid.ItemsSource = null;
            StatusTextBlock.Text = "Could not load Active Directory computers.";
            MessageBox.Show(this, ex.Message, "Could not load domain computers", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            AddButton.IsEnabled = true;
        }
    }

    private void PopulateOperatingSystemFilter()
    {
        var previous = GetSelectedContent(OperatingSystemFilterComboBox);
        OperatingSystemFilterComboBox.Items.Clear();
        OperatingSystemFilterComboBox.Items.Add(new ComboBoxItem { Content = "All OS versions" });
        foreach (var value in _domainComputers.Select(computer => computer.OperatingSystemDisplay)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            OperatingSystemFilterComboBox.Items.Add(new ComboBoxItem { Content = value });

        OperatingSystemFilterComboBox.SelectedItem = OperatingSystemFilterComboBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), previous, StringComparison.OrdinalIgnoreCase));
        OperatingSystemFilterComboBox.SelectedIndex = Math.Max(0, OperatingSystemFilterComboBox.SelectedIndex);
    }

    private void FilterChanged(object sender, EventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        _computerView?.Refresh();
        if (_computerView is null) return;
        var shown = _computerView.Cast<object>().Count();
        StatusTextBlock.Text = $"Showing {shown} of {_domainComputers.Count} enabled computer(s). Click a heading to sort; Ctrl+click or Shift+click to select.";
    }

    private bool MatchesFilter(object item)
    {
        if (item is not DomainComputer computer) return false;
        var selectedOs = GetSelectedContent(OperatingSystemFilterComboBox);
        if (!string.IsNullOrWhiteSpace(selectedOs) && selectedOs != "All OS versions" &&
            !computer.OperatingSystemDisplay.Equals(selectedOs, StringComparison.OrdinalIgnoreCase))
            return false;

        var query = SearchTextBox.Text.Trim();
        if (query.Length == 0) return true;
        return SearchFieldComboBox.SelectedIndex switch
        {
            1 => computer.TargetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 computer.DnsHostName.Contains(query, StringComparison.OrdinalIgnoreCase),
            2 => computer.OperatingSystem.Contains(query, StringComparison.OrdinalIgnoreCase),
            3 => computer.OperatingSystemVersion.Contains(query, StringComparison.OrdinalIgnoreCase),
            _ => computer.TargetName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 computer.DnsHostName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 computer.OperatingSystem.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 computer.OperatingSystemVersion.Contains(query, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string GetSelectedContent(ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

    private void SelectAllButton_Click(object sender, RoutedEventArgs e) => ComputerGrid.SelectAll();

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedComputers = ComputerGrid.SelectedItems.Cast<DomainComputer>().ToList();
        if (SelectedComputers.Count == 0)
        {
            MessageBox.Show(this, "Select at least one domain computer.", "No computers selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
