using System.Text.Json;

namespace TaskbarDimmer;

internal sealed class Settings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Keep the taskbar dimmed all the time, even while the mouse is over it.</summary>
    public bool AlwaysDim { get; set; }

    /// <summary>UI language: "en" (default) or "de".</summary>
    public string Language { get; set; } = Strings.English;

    /// <summary>Overlay opacity when dimmed (0 = invisible, 1 = fully black).</summary>
    public double DimLevel { get; set; } = 0.85;

    /// <summary>Seconds of inactivity before the taskbar starts dimming.</summary>
    public int DelaySeconds { get; set; } = 5;

    /// <summary>Duration of the dimming animation in milliseconds.</summary>
    public int FadeOutMs { get; set; } = 1500;

    /// <summary>Duration of the reveal animation in milliseconds.</summary>
    public int FadeInMs { get; set; } = 120;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskbarDimmer", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch
        {
            // Corrupt or unreadable settings fall back to defaults.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Failing to persist settings is not fatal.
        }
    }
}
