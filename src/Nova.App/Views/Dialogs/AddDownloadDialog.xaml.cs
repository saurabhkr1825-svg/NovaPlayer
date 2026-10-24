using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Nova.Core.Common;

namespace Nova.App.Views.Dialogs;

public partial class AddDownloadDialog : Window
{
    public string ValidatedUrl { get; private set; } = string.Empty;
    public string DestinationFolder { get; private set; } = string.Empty;
    public string? CustomFileName { get; private set; }

    public AddDownloadDialog(string defaultFolder, string? defaultUrl = null)
    {
        InitializeComponent();

        DestinationFolder = defaultFolder;
        DestFolderTextBox.Text = defaultFolder;

        if (!string.IsNullOrWhiteSpace(defaultUrl))
        {
            UrlTextBox.Text = defaultUrl;
            UrlTextBox.SelectAll();
        }

        Loaded += (s, e) => UrlTextBox.Focus();
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Destination Folder — NovaPlayer",
            InitialDirectory = DestFolderTextBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            DestFolderTextBox.Text = dialog.FolderName;
            DestinationFolder = dialog.FolderName;
        }
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        TryValidateAndSubmit();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryValidateAndSubmit();
            e.Handled = true;
        }
    }

    private void TryValidateAndSubmit()
    {
        var urlInput = UrlTextBox.Text.Trim();
        var validation = SecurityValidator.ValidateUrlStructure(urlInput);
        if (validation.IsFailure)
        {
            ErrorLabel.Text = validation.Error;
            ErrorLabel.Visibility = Visibility.Visible;
            return;
        }

        var folderInput = DestFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folderInput))
        {
            ErrorLabel.Text = "Destination directory cannot be empty.";
            ErrorLabel.Visibility = Visibility.Visible;
            return;
        }

        ValidatedUrl = validation.Value.AbsoluteUri;
        DestinationFolder = folderInput;
        CustomFileName = string.IsNullOrWhiteSpace(FileNameTextBox.Text)
            ? null
            : FileNameTextBox.Text.Trim();

        DialogResult = true;
        Close();
    }
}
