using System.Globalization;
using System.Windows;

namespace WinLayout.Services;

public static class LocalizationService
{
    private const string DefaultCulture = "zh-CN";

    public static string CurrentCulture { get; private set; } = DefaultCulture;

    public static void Initialize()
    {
        var sysCulture = CultureInfo.CurrentUICulture.Name;
        if (sysCulture.StartsWith("en"))
            CurrentCulture = "en-US";
        else
            CurrentCulture = "zh-CN";

        ApplyCulture(CurrentCulture);
    }

    public static void SetCulture(string culture)
    {
        CurrentCulture = culture;
        ApplyCulture(culture);
    }

    private static void ApplyCulture(string culture)
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Strings.{culture}.xaml")
        };

        // Replace existing language resource
        var app = Application.Current;
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Strings.") == true);
        if (existing != null)
            app.Resources.MergedDictionaries.Remove(existing);

        app.Resources.MergedDictionaries.Add(dict);
    }

    public static string GetString(string key)
    {
        return Application.Current.TryFindResource(key) as string ?? key;
    }
}
