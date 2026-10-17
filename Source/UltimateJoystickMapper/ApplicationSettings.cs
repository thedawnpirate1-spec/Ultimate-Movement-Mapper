using System.Text.Json;

namespace UltimateJoystickMapper;

public sealed class ApplicationSettings
{
    public string LastProfileName { get; set; } = "Default";
    public bool AutoStartMovement { get; set; }
    public bool FilterKeysEnabled { get; set; }
    public int FilterKeysDelayMs { get; set; } = 150;
    public int FilterKeysRepeatMs { get; set; } = 15;
    // Windows' own values before the tweak was first switched on, restored when it is switched off.
    public Tweaks.FilterKeysState? FilterKeysOriginal { get; set; }
    public bool DisableAccessibilityShortcuts { get; set; }
    public Tweaks.AccessibilityShortcutState? AccessibilityShortcutsOriginal { get; set; }
    public bool HighPriority { get; set; }
    public bool BlockWinKeyInGame { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GMKRebuild", "app-settings.json");

    public static ApplicationSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(FilePath)) ?? new ApplicationSettings();
        }
        catch { }
        return new ApplicationSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
