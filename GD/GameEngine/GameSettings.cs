public class GameSettings
{
    private const string SettingsFile = "settings.json";

    // Display
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public bool IsFullScreen { get; set; } = false;
    public int TargetFPS { get; set; } = 60;

    // Audio
    public float MasterVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 0.5f;

    public static GameSettings Load()
    {
        if (!File.Exists(SettingsFile))
        {
            var defaultSettings = new GameSettings();
            defaultSettings.Save();
            return defaultSettings;
        }

        var json = File.ReadAllText(SettingsFile);
        return JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings();
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(SettingsFile, json);
    }
}

