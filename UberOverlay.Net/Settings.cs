using System.IO;
using System.Text.Json;

namespace UberOverlay;

public class Settings
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double Opacity { get; set; } = 0.85;
    public bool ClickThrough { get; set; }
    /// <summary>"auto" (langue de Windows), "fr", "en" ou "es".</summary>
    public string Language { get; set; } = "auto";
    /// <summary>"auto" (format de Windows), "24" ou "12".</summary>
    public string TimeFormat { get; set; } = "auto";

    public static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UberOverlay");
    static string FilePath => Path.Combine(Folder, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
        catch { return new Settings(); }
    }

    public void Save()
    {
        try { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); }
        catch { }
    }
}
