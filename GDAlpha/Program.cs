// Start GlobalUsings.cs
global using Raylib_cs;
global using System.Collections.Generic;
global using System.IO;
global using System.Text.Json;
global using System;
// End GlobalUsings.cs

// Start ./Program.cs
using var engine = new GameEngine();
engine.Run();
// End ./Program.cs

// Start ./GameEngine/SceneManager.cs
public class SceneManager
{
    private IScene _currentScene;
    private readonly GameEngine _engine;

    public SceneManager(GameEngine engine)
    {
        _engine = engine;
    }

    public void ChangeScene(IScene newScene)
    {
        // 1. Clean up the current scene if one exists
        _currentScene?.Unload();

        // 2. Switch to the new scene
        _currentScene = newScene;

        // 3. Initialize the new scene, injecting the engine (Assets/Settings)
        _currentScene?.Init(_engine);
    }

    public void Update(float deltaTime)
    {
        _currentScene?.Update(deltaTime);
    }

    public void Draw()
    {
        _currentScene?.Draw();
    }

    public void UnloadCurrent()
    {
        _currentScene?.Unload();
        _currentScene = null;
    }
}
// End ./GameEngine/SceneManager.cs

// Start ./GameEngine/GameSettings.cs
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
// End ./GameEngine/GameSettings.cs

// Start ./GameEngine/AssetManager.cs
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
// End ./GameEngine/AssetManager.cs

// Start ./GameEngine/GameEngine.cs
public class GameEngine : IDisposable
{
    public GameSettings Settings { get; private set; }
    public AssetManager Assets { get; private set; }
    public SceneManager Scenes { get; private set; }

    public GameEngine()
    {
        Settings = GameSettings.Load();
    }

    public void Run()
    {
        Initialize();
        GameLoop();
    }

    private void Initialize()
    {
        Raylib.InitWindow(Settings.Width, Settings.Height, "Custom C# Game Engine");
        if (Settings.IsFullScreen) Raylib.ToggleFullscreen();
        Raylib.SetTargetFPS(Settings.TargetFPS);

        Raylib.InitAudioDevice();
        Raylib.SetMasterVolume(Settings.MasterVolume);

        Assets = new AssetManager();
        Assets.LoadAll();

        // Initialize SceneManager and load the first scene
        Scenes = new SceneManager(this);
        Scenes.ChangeScene(new TestScene());
    }

    private void GameLoop()
    {
        while (!Raylib.WindowShouldClose())
        {
            float deltaTime = Raylib.GetFrameTime();
            Update(deltaTime);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);
            Draw();
            Raylib.EndDrawing();
        }
    }

    private void Update(float deltaTime)
    {
        // Only update the single currently playing audio stream (Highly optimized)
        Assets.UpdateAudio();

        // Route updates to the active scene
        Scenes.Update(deltaTime);
    }

    private void Draw()
    {
        // Route drawing to the active scene
        Scenes.Draw();
    }

    public void Dispose()
    {
        // Unload the current scene before killing assets
        Scenes?.UnloadCurrent();

        Assets?.Dispose();

        if (Raylib.IsAudioDeviceReady()) Raylib.CloseAudioDevice();
        if (Raylib.IsWindowReady()) Raylib.CloseWindow();

        Settings.Save();
        GC.SuppressFinalize(this);
    }
}
// End ./GameEngine/GameEngine.cs

// Start ./Scenes/IScene.cs
public interface IScene
{
    void Init(GameEngine engine);
    void Update(float deltaTime);
    void Draw();
    void Unload(); // Called automatically when switching away from this scene
}
// End ./Scenes/IScene.cs

// Start ./Scenes/TextScene.cs
public class TestScene : IScene
{
    private GameEngine _engine;
    private float _timer;

    public void Init(GameEngine engine)
    {
        _engine = engine;
        _timer = 0f;

        // If you had a music file named "theme.mp3" in Assets/Sounds/BGM:
        // _engine.Assets.PlayBgm("theme");
    }

    public void Update(float deltaTime)
    {
        _timer += deltaTime;

        // Example input handling
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            // If you had a sound effect named "jump.wav" in Assets/Sounds/SFX:
            // if (_engine.Assets.Sfx.TryGetValue("jump", out var jumpSound))
            //     Raylib.PlaySound(jumpSound);
        }
    }

    public void Draw()
    {
        Raylib.DrawText("Engine Phase 3: Scene Manager Active!", 20, 80, 20, Color.DarkBlue);
        Raylib.DrawText($"Time in scene: {_timer:F2} seconds", 20, 110, 20, Color.DarkGray);
        Raylib.DrawText("Press Space to test input. Press ESC to quit.", 20, 140, 20, Color.DarkGray);
    }

    public void Unload()
    {
        // Clean up scene-specific data here when switching away
    }
}
// End ./Scenes/TextScene.cs

