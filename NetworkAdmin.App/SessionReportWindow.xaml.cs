using System.Windows;

namespace NetworkAdmin.App;

public partial class SessionReportWindow : Window
{
    public SessionReportWindow() => InitializeComponent();

    public void ShowReport(string html) => ReportBrowser.NavigateToString(html);
}
