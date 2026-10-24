using System.Windows;
using System.Windows.Input;
using Nova.Core.Common;

namespace Nova.App.Views.Dialogs;

public partial class OpenUrlDialog : Window
{
    public string ValidatedUrl { get; private set; } = string.Empty;

    public OpenUrlDialog(string? defaultUrl = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(defaultUrl))
        {
            UrlTextBox.Text = defaultUrl;
            UrlTextBox.SelectAll();
        }
        Loaded += (s, e) => UrlTextBox.Focus();
    }

    private void OnPlayClick(object sender, RoutedEventArgs e)
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
        var input = UrlTextBox.Text.Trim();
        var validation = UrlValidator.ValidateMediaUrl(input);
        if (validation.IsFailure)
        {
            ErrorLabel.Text = validation.Error;
            ErrorLabel.Visibility = Visibility.Visible;
            return;
        }

        ValidatedUrl = validation.Value.AbsoluteUri;
        DialogResult = true;
        Close();
    }
}
