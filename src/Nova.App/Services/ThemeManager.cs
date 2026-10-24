using System.Windows;
using Serilog;

namespace Nova.App.Services;

public static class ThemeManager
{
    public const string DarkTheme = "Dark";
    public const string LightTheme = "Light";

    public static string CurrentTheme { get; private set; } = DarkTheme;

    public static event EventHandler<string>? ThemeChanged;

    public static void ApplyTheme(string themeName)
    {
        if (Application.Current == null) return;

        var targetTheme = themeName.Equals(LightTheme, StringComparison.OrdinalIgnoreCase) ? LightTheme : DarkTheme;
        var themeUri = new Uri($"Themes/{targetTheme}Theme.xaml", UriKind.Relative);

        try
        {
            var newDict = new ResourceDictionary { Source = themeUri };
            var appResources = Application.Current.Resources;

            // Find existing theme dictionary
            ResourceDictionary? existingThemeDict = null;
            foreach (var mergedDict in appResources.MergedDictionaries)
            {
                if (mergedDict.Source != null && mergedDict.Source.OriginalString.Contains("Theme.xaml"))
                {
                    existingThemeDict = mergedDict;
                    break;
                }
            }

            if (existingThemeDict != null)
            {
                var index = appResources.MergedDictionaries.IndexOf(existingThemeDict);
                appResources.MergedDictionaries[index] = newDict;
            }
            else
            {
                appResources.MergedDictionaries.Add(newDict);
            }

            CurrentTheme = targetTheme;
            ThemeChanged?.Invoke(null, targetTheme);
            Log.Information("Applied runtime theme: {Theme}", targetTheme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply runtime theme: {Theme}", targetTheme);
        }
    }

    public static void ToggleTheme()
    {
        var newTheme = CurrentTheme == DarkTheme ? LightTheme : DarkTheme;
        ApplyTheme(newTheme);
    }
}
