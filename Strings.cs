namespace Duskbar;

/// <summary>UI texts in English (default) and German.</summary>
internal static class Strings
{
    public const string English = "en";
    public const string German = "de";

    public static string Language { get; set; } = English;

    private static readonly Dictionary<string, (string En, string De)> Texts = new()
    {
        ["Tooltip"] = ("Duskbar", "Duskbar"),
        ["TooltipPaused"] = ("Duskbar (paused)", "Duskbar (pausiert)"),
        ["Active"] = ("Enabled", "Aktiv"),
        ["Mode"] = ("Mode", "Modus"),
        ["ModeIdle"] = ("Dim when idle", "Bei Inaktivität dimmen"),
        ["ModeAlways"] = ("Always dimmed", "Dauerhaft gedimmt"),
        ["DimLevel"] = ("Dim level", "Abdunkelung"),
        ["Delay"] = ("Delay", "Verzögerung"),
        ["Seconds"] = ("{0} seconds", "{0} Sekunden"),
        ["Autostart"] = ("Start with Windows", "Mit Windows starten"),
        ["Language"] = ("Language", "Sprache"),
        ["Exit"] = ("Exit", "Beenden"),
    };

    public static string T(string key) =>
        Texts.TryGetValue(key, out var t) ? (Language == German ? t.De : t.En) : key;

    public static string T(string key, object arg) => string.Format(T(key), arg);
}
