public class AssetManager : IDisposable
{
    // Texture Dictionaries
    public Dictionary<string, Texture2D> Sprites { get; } = [];
    public Dictionary<string, Texture2D> Tiles { get; } = [];
    public Dictionary<string, Texture2D> Backgrounds { get; } = [];

    // Audio Dictionaries
    public Dictionary<string, Sound> Sfx { get; } = [];
    public Dictionary<string, Music> Bgm { get; } = [];

    // Track the currently playing music for optimized updates
    private Music? _currentBgm;

    private readonly GameSettings _settings;

    public AssetManager(GameSettings settings)
    {
        _settings = settings;
    }

    public void LoadAll()
    {
        // Note: Raylib.InitWindow() and InitAudioDevice() MUST be called before this!
        LoadTexturesToDictionary(AssetPath("Textures/Sprites"), Sprites);
        LoadTexturesToDictionary(AssetPath("Textures/Tiles"), Tiles);
        LoadTexturesToDictionary(AssetPath("Textures/Backgrounds"), Backgrounds);

        LoadSoundsToDictionary(AssetPath("Sounds/SFX"), Sfx);
        LoadMusicToDictionary(AssetPath("Sounds/BGM"), Bgm);
    }

    // Assets are copied next to the exe, so resolve them there rather than against
    // the working directory, which differs between `dotnet run` and a direct launch.
    private static string AssetPath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", relativePath);

    private void LoadTexturesToDictionary(string folderPath, Dictionary<string, Texture2D> dict)
    {
        if (!Directory.Exists(folderPath)) return;

        foreach (var file in Directory.GetFiles(folderPath))
        {
            if (IsImageFile(file))
            {
                string key = Path.GetFileNameWithoutExtension(file);
                dict[key] = Raylib.LoadTexture(file);
                Console.WriteLine($"Loaded Texture: {key} into {Path.GetFileName(folderPath)}");
            }
        }
    }

    private void LoadSoundsToDictionary(string folderPath, Dictionary<string, Sound> dict)
    {
        if (!Directory.Exists(folderPath)) return;

        foreach (var file in Directory.GetFiles(folderPath))
        {
            if (IsAudioFile(file))
            {
                string key = Path.GetFileNameWithoutExtension(file);
                dict[key] = Raylib.LoadSound(file);
                Console.WriteLine($"Loaded SFX: {key}");
            }
        }
    }

    private void LoadMusicToDictionary(string folderPath, Dictionary<string, Music> dict)
    {
        if (!Directory.Exists(folderPath)) return;

        foreach (var file in Directory.GetFiles(folderPath))
        {
            if (IsAudioFile(file))
            {
                string key = Path.GetFileNameWithoutExtension(file);
                dict[key] = Raylib.LoadMusicStream(file);
                Console.WriteLine($"Loaded BGM: {key}");
            }
        }
    }

    // --- Audio Control ---

    public void PlayBgm(string key)
    {
        // Stop the old music if something is already playing
        if (_currentBgm.HasValue)
        {
            Raylib.StopMusicStream(_currentBgm.Value);
        }

        if (!Bgm.TryGetValue(key, out var music))
        {
            // Clear the handle too. Leaving the stopped stream in place meant
            // UpdateAudio kept pumping a track that is no longer playing, and a
            // later StopBgm would stop something the caller never started.
            _currentBgm = null;
            Console.WriteLine($"Error: Could not find BGM with key '{key}'");
            return;
        }

        Raylib.SetMusicVolume(music, _settings.MusicVolume);
        Raylib.PlayMusicStream(music);
        _currentBgm = music;
    }

    public void PlaySfx(string key)
    {
        if (Sfx.TryGetValue(key, out var sound))
        {
            Raylib.SetSoundVolume(sound, _settings.SfxVolume);
            Raylib.PlaySound(sound);
        }
        else
        {
            Console.WriteLine($"Error: Could not find SFX with key '{key}'");
        }
    }

    // Re-applies the current volume settings to live audio (e.g. after the settings menu changes them).
    public void ApplyVolumeSettings()
    {
        Raylib.SetMasterVolume(_settings.MasterVolume);

        if (_currentBgm.HasValue)
        {
            Raylib.SetMusicVolume(_currentBgm.Value, _settings.MusicVolume);
        }
    }

    public void StopBgm()
    {
        if (_currentBgm.HasValue)
        {
            Raylib.StopMusicStream(_currentBgm.Value);
            _currentBgm = null;
        }
    }

    public void UpdateAudio()
    {
        // Highly optimized: Only update the single stream that is actually playing
        if (_currentBgm.HasValue)
        {
            Raylib.UpdateMusicStream(_currentBgm.Value);
        }
    }

    // --- Helpers ---

    private bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp";
    }

    private bool IsAudioFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext is ".wav" or ".ogg" or ".mp3";
    }

    // --- Cleanup ---

    public void Dispose()
    {
        // Unload Textures from VRAM
        foreach (var texture in Sprites.Values) Raylib.UnloadTexture(texture);
        foreach (var texture in Tiles.Values) Raylib.UnloadTexture(texture);
        foreach (var texture in Backgrounds.Values) Raylib.UnloadTexture(texture);

        // Unload Audio from Audio Device
        foreach (var sound in Sfx.Values) Raylib.UnloadSound(sound);
        foreach (var music in Bgm.Values) Raylib.UnloadMusicStream(music);

        Sprites.Clear();
        Tiles.Clear();
        Backgrounds.Clear();
        Sfx.Clear();
        Bgm.Clear();

        Console.WriteLine("All unmanaged assets successfully disposed.");
    }
}

