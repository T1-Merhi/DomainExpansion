public class MainMenuScene : IScene
{
    public event Action<GameEvent>? EventRaised;

    private static readonly string[] MainOptions = ["Play", "Settings", "Quit"];
    private const float VolumeStep = 0.1f;

    private AssetManager _assets;
    private GameSettings _settings;
    private int _selectedIndex;
    private bool _inSettings;

    public void Init(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;
        _selectedIndex = 0;
        _inSettings = false;

        _assets.PlayBgm("intro");
    }

    public void Update(float deltaTime)
    {
        if (_inSettings) UpdateSettingsMenu();
        else UpdateMainMenu();
    }

    private void UpdateMainMenu()
    {
        HandleVerticalNavigation(MainOptions.Length);

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            switch (_selectedIndex)
            {
                case 0: EventRaised?.Invoke(GameEvent.PlayRequested); break;
                case 1: _inSettings = true; _selectedIndex = 0; break;
                case 2: EventRaised?.Invoke(GameEvent.QuitRequested); break;
            }
        }
    }

    private void UpdateSettingsMenu()
    {
        // Fullscreen, Master Volume, Music Volume, SFX Volume, Back
        HandleVerticalNavigation(5);

        bool confirm = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);

        float delta = 0f;
        if (Raylib.IsKeyPressed(KeyboardKey.Left)) delta = -VolumeStep;
        else if (Raylib.IsKeyPressed(KeyboardKey.Right)) delta = VolumeStep;

        switch (_selectedIndex)
        {
            case 0:
                if (confirm)
                {
                    _settings.IsFullScreen = !_settings.IsFullScreen;
                    Raylib.ToggleFullscreen();
                }
                break;

            case 1:
                if (delta != 0f) _settings.MasterVolume = Adjust(_settings.MasterVolume, delta);
                break;

            case 2:
                if (delta != 0f) _settings.MusicVolume = Adjust(_settings.MusicVolume, delta);
                break;

            case 3:
                if (delta != 0f) _settings.SfxVolume = Adjust(_settings.SfxVolume, delta);
                break;

            case 4:
                if (confirm)
                {
                    _settings.Save();
                    _inSettings = false;
                    _selectedIndex = 1; // land back on "Settings" in the main list
                }
                break;
        }

        // Push the new levels onto anything currently audible so the change is heard immediately.
        if (delta != 0f) _assets.ApplyVolumeSettings();
    }

    private static float Adjust(float value, float delta) => Math.Clamp(value + delta, 0f, 1f);

    private void HandleVerticalNavigation(int optionCount)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Down))
            _selectedIndex = (_selectedIndex + 1) % optionCount;
        else if (Raylib.IsKeyPressed(KeyboardKey.Up))
            _selectedIndex = (_selectedIndex - 1 + optionCount) % optionCount;
    }

    public void Draw()
    {
        if (_inSettings) DrawSettingsMenu();
        else DrawMainMenu();
    }

    private void DrawMainMenu()
    {
        Raylib.DrawText("MAIN MENU", 20, 40, 30, Color.DarkBlue);

        for (int i = 0; i < MainOptions.Length; i++)
        {
            var color = i == _selectedIndex ? Color.Red : Color.DarkGray;
            Raylib.DrawText(MainOptions[i], 40, 120 + i * 40, 24, color);
        }

        Raylib.DrawText("UP/DOWN to navigate, ENTER to select", 20, 280, 16, Color.Gray);
    }

    private void DrawSettingsMenu()
    {
        Raylib.DrawText("SETTINGS", 20, 40, 30, Color.DarkBlue);

        string[] labels =
        [
            $"Fullscreen: {(_settings.IsFullScreen ? "On" : "Off")}",
            $"Master Volume: {_settings.MasterVolume:P0}",
            $"Music Volume: {_settings.MusicVolume:P0}",
            $"SFX Volume: {_settings.SfxVolume:P0}",
            "Back",
        ];

        for (int i = 0; i < labels.Length; i++)
        {
            var color = i == _selectedIndex ? Color.Red : Color.DarkGray;
            Raylib.DrawText(labels[i], 40, 120 + i * 40, 24, color);
        }

        Raylib.DrawText("LEFT/RIGHT to adjust, ENTER to select", 20, 280, 16, Color.Gray);
    }

    public void Unload()
    {
        _assets.StopBgm();
    }
}
