using System.Globalization;
using System.Windows.Data;
using Nova.Core.Common;

namespace Nova.App.Converters;

public class MillisecondsToTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long ms)
        {
            return TimeFormatter.FormatMilliseconds(ms);
        }
        if (value is int intMs)
        {
            return TimeFormatter.FormatMilliseconds(intMs);
        }
        return "00:00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
