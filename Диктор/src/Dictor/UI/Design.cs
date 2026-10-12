using System.Windows;
using System.Windows.Media;
namespace Dictor.UI;

internal static class Design
{
    public static void Apply(Window window, string theme)
    {
        if (window.Resources.MergedDictionaries.Count == 0)
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Dictor;component/UI/Theme.xaml", UriKind.Relative) });
        bool dark = theme == "Dark" || theme == "System" && (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) as int? == 0);
        var colors = new Dictionary<string, string>
        {
            ["Canvas"] = dark ? "#151C26" : "#EEF2F7",
            ["Surface"] = dark ? "#202937" : "#FFFFFF",
            ["Ink"] = dark ? "#E8EEF7" : "#182435",
            ["Muted"] = dark ? "#A9B8CE" : "#52647C",
            ["Line"] = dark ? "#3B475A" : "#D7DFEB",
            ["Accent"] = dark ? "#83A9FF" : "#245DD8",
            ["Soft"] = dark ? "#293D60" : "#E7EFFF",
            ["Recording"] = dark ? "#FF8998" : "#C53446"
        };
        foreach (var (key, color) in colors) window.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        window.FontFamily = new FontFamily("Segoe UI"); window.FontSize = 13;
        window.SetResourceReference(Window.ForegroundProperty, "Ink");
    }
}
