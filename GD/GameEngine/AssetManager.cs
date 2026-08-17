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

    public void LoadAll()
    {
        // Note: Raylib.InitWindow() and InitAudioDevice() MUST be called before this!
        LoadTexturesToDictionary("Assets/Textures/Sprites", Sprites);
        LoadTexturesToDictionary("Assets/Textures/Tiles", Tiles);
        LoadTexturesToDictionary("Assets/Textures/Backgrounds", Backgrounds);

        LoadSoundsToDictionary("Assets/Sounds/SFX", Sfx);
        LoadMusicToDictionary("Assets/Sounds/BGM", Bgm);
    }

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

        // Start the new music
        if (Bgm.TryGetValue(key, out var music))
        {
            Raylib.PlayMusicStream(music);
            _currentBgm = music;
        }
        else
        {
            Console.WriteLine($"Error: Could not find BGM with key '{key}'");
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

