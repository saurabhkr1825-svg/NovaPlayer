using System.Windows;
using System.Windows.Input;

namespace Nova.App.Views.Dialogs;

public partial class InputDialog : Window
{
    public string InputText => InputTextBox.Text.Trim();

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        InputTextBox.Text = defaultValue;
        InputTextBox.SelectAll();
        Loaded += (s, e) => InputTextBox.Focus();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
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
            DialogResult = true;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
