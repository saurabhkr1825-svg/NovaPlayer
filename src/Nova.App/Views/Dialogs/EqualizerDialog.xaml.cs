using System.Windows;
using Nova.App.ViewModels;

namespace Nova.App.Views.Dialogs;

public partial class EqualizerDialog : Window
{
    public EqualizerDialog(EqualizerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
