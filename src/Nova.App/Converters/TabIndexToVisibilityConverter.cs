using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nova.App.Converters;

public class TabIndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentTab && parameter != null && int.TryParse(parameter.ToString(), out int targetTab))
        {
            return currentTab == targetTab ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
