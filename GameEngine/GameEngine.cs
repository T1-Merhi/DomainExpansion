public class GameEngine : IDisposable
{
    public GameSettings Settings { get; private set; }
    public AssetManager Assets { get; private set; }
    public SceneManager Scenes { get; private set; }

    private bool _shouldQuit;

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
        // Config must exist before anything reads Tuning, which Player does in
        // its field initialisers.
        ConfigStore.Current.Reload();

        Raylib.InitWindow(Settings.Width, Settings.Height,
            AppMode.IsAdmin ? "Domain Expansion - ADMIN" : "Domain Expansion");

        // Apply after the window exists, so the framebuffer is resized to the
        // monitor rather than stretched from the windowed size.
        Display.Apply(Settings);

        Raylib.SetTargetFPS(Settings.TargetFPS);

        // Scenes bind ESC themselves (back to menu), so stop Raylib closing the
        // window on it. Quitting goes through the main menu or the window button.
        Raylib.SetExitKey(KeyboardKey.Null);

        Raylib.InitAudioDevice();

        Assets = new AssetManager(Settings);
        Assets.LoadAll();
        Assets.ApplyVolumeSettings();

        // Initialize SceneManager and load the first scene
        Scenes = new SceneManager(Assets, Settings);
        Scenes.QuitRequested += () => _shouldQuit = true;
        Scenes.ChangeScene(new MainMenuScene());
    }

    private void GameLoop()
    {
        while (!Raylib.WindowShouldClose() && !_shouldQuit)
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

