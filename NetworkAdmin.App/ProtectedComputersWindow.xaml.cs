using System.Windows;
using NetworkAdmin.App.Services;

namespace NetworkAdmin.App;

public partial class ProtectedComputersWindow : Window
{
    public string ComputerNames => NamesTextBox.Text;

    public ProtectedComputersWindow(IEnumerable<string> computerNames)
    {
        InitializeComponent();
        WindowSizingService.RememberPlacement(this, "ProtectedComputersWindow");
        NamesTextBox.Text = string.Join(Environment.NewLine, computerNames);
        NamesTextBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
