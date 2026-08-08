using System.IO;
using System.Text.Json;
using System.Windows;

namespace DuplicateFinder.App.Helpers;

public class AppConfig
{
    public List<string> ScanPaths { get; set; } = new();
    public bool IsLightTheme { get; set; }

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DupliKiller",
        "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}

internal static class ThemeManager
{
    private const string DarkTheme = "Resources/Styles.xaml";
    private const string LightTheme = "Resources/StylesLight.xaml";

    public static void SetTheme(bool isLight)
    {
        var dicts = System.Windows.Application.Current.Resources.MergedDictionaries;
        var uri = isLight ? LightTheme : DarkTheme;
        var existing = dicts.FirstOrDefault(d =>
            d.Source != null && (d.Source.OriginalString == DarkTheme || d.Source.OriginalString == LightTheme));
        if (existing != null)
            dicts.Remove(existing);
        dicts.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
    }
}
