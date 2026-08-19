public class GameSettings
{
    // Resolved against the app directory, not the working directory, so `dotnet run`
    // and the published exe always read and write the same file.
    private static readonly string SettingsFile =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    // Display
    //
    // Width/Height are the preferred WINDOWED size, not the live framebuffer.
    // While fullscreen the window matches the monitor and these keep the size
    // to restore on exit. Use Raylib.GetScreenWidth/Height for actual
    // dimensions at runtime.
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public bool IsFullScreen { get; set; } = false;
    public int TargetFPS { get; set; } = 60;

    // Audio
    public float MasterVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 0.5f;

    /// <summary>
    /// Never throws. This runs before the window exists, so an unhandled
    /// exception here means the game fails to start with no visible reason and
    /// no obvious recovery beyond finding and deleting the file.
    /// </summary>
    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);

                var loaded = JsonSerializer.Deserialize<GameSettings>(json);
                if (loaded != null) return loaded.Validated();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Settings: could not load ({ex.Message}); restoring defaults");
        }

        // Rewrite with defaults, so a corrupt file does not fail again next launch.
        var defaults = new GameSettings();
        defaults.Save();
        return defaults;
    }

    /// <summary>
    /// Repairs values that would break the window or audio device. A hand-edited
    /// zero resolution is otherwise fatal at InitWindow.
    /// </summary>
    private GameSettings Validated()
    {
        if (Width < 320) Width = 1280;
        if (Height < 240) Height = 720;
        if (TargetFPS < 10) TargetFPS = 60;

        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
        SfxVolume = Math.Clamp(SfxVolume, 0f, 1f);
        MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);

        return this;
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(SettingsFile, json);
    }
}

