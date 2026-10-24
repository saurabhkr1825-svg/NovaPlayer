using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using Serilog;

namespace Nova.App.Views.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open link: {Uri}", e.Uri);
        }
    }

    private void OnOpenLicensesFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var licensePath = Path.Combine(baseDir, "THIRD_PARTY_LICENSES.txt");
            if (!File.Exists(licensePath))
            {
                licensePath = Path.Combine(baseDir, "THIRD_PARTY_LICENSES.md");
            }

            if (File.Exists(licensePath))
            {
                Process.Start(new ProcessStartInfo(licensePath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(
                    "Third-party license documentation is located in the application installation directory and source repository.",
                    "License Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open license file.");
        }
    }
}
