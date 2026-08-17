public class MainMenuScene : IScene
{
    public event Action<GameEvent>? EventRaised;

    private static readonly string[] MainOptions = ["Play", "Settings", "Quit"];

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
        // Fullscreen, Master Volume, Back
        HandleVerticalNavigation(3);

        if (_selectedIndex == 0 && Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            _settings.IsFullScreen = !_settings.IsFullScreen;
            Raylib.ToggleFullscreen();
        }
        else if (_selectedIndex == 1 && Raylib.IsKeyPressed(KeyboardKey.Left))
        {
            _settings.MasterVolume = Math.Max(0f, _settings.MasterVolume - 0.1f);
            Raylib.SetMasterVolume(_settings.MasterVolume);
        }
        else if (_selectedIndex == 1 && Raylib.IsKeyPressed(KeyboardKey.Right))
        {
            _settings.MasterVolume = Math.Min(1f, _settings.MasterVolume + 0.1f);
            Raylib.SetMasterVolume(_settings.MasterVolume);
        }
        else if (_selectedIndex == 2 && (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space)))
        {
            _settings.Save();
            _inSettings = false;
            _selectedIndex = 1; // land back on "Settings" in the main list
        }
    }

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
    }
}
