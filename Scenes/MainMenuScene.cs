public class MainMenuScene : IScene
{
    public event Action<GameEvent> EventRaised;

    /// <summary>Volume change per second while an arrow key is held.</summary>
    private const float VolumeStep = 0.6f;

    // Layout, in pixels. Rows are laid out from a centred panel so everything
    // recentres automatically when the resolution changes.
    private const int PanelWidth = 460;
    private const int RowHeight = 52;
    private const int RowGap = 10;

    private static readonly string[] PlayerOptions = ["Play", "Settings", "Quit"];
    private static readonly string[] AdminOptions = ["Play", "Test Arena", "Config Editor", "Settings", "Quit"];

    /// <summary>Admin entries are absent from the array itself in player mode,
    /// so they cannot be selected, drawn or reached by index.</summary>
    private static string[] MainOptions => AppMode.IsAdmin ? AdminOptions : PlayerOptions;

    private enum Page { Main, Settings }

    // Settings rows, in display order.
    private const int RowFullscreen = 0;
    private const int RowMaster = 1;
    private const int RowMusic = 2;
    private const int RowSfx = 3;
    private const int RowBack = 4;
    private const int SettingsRowCount = 5;

    private AssetManager _assets;
    private GameSettings _settings;

    private Page _page;
    private int _selected;
    private bool _draggingSlider;

    // Reloaded on entry rather than cached, so returning from a run that set a
    // new high score shows it immediately.
    private Leaderboard _leaderboard;

    public void Init(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;
        _page = Page.Main;
        _selected = 0;
        _draggingSlider = false;
        _leaderboard = Leaderboard.Load();
    }

    // --- Layout ---------------------------------------------------------

    private static int CentreX => Raylib.GetScreenWidth() / 2;

    private static int FirstRowY(int rowCount)
    {
        int block = rowCount * RowHeight + (rowCount - 1) * RowGap;
        return Raylib.GetScreenHeight() / 2 - block / 2 + 30;
    }

    private static Rectangle RowRect(int index, int rowCount)
    {
        int y = FirstRowY(rowCount) + index * (RowHeight + RowGap);
        return new Rectangle(CentreX - PanelWidth / 2f, y, PanelWidth, RowHeight);
    }

    /// <summary>Right-hand control area of a settings row, where the widget sits.</summary>
    private static Rectangle ControlRect(int index)
    {
        Rectangle row = RowRect(index, SettingsRowCount);
        const float controlWidth = 220f;
        return new Rectangle(row.X + row.Width - controlWidth, row.Y, controlWidth, row.Height);
    }

    // --- Update ---------------------------------------------------------

    public void Update(float deltaTime)
    {
        // Drawing performs the interaction, since these are immediate-mode
        // widgets. Update only handles keyboard navigation.
        if (_page == Page.Main) UpdateMainKeys();
        else UpdateSettingsKeys();
    }

    private void UpdateMainKeys()
    {
        Navigate(MainOptions.Length);

        if (Confirmed()) Activate(_selected);
    }

    private void UpdateSettingsKeys()
    {
        Navigate(SettingsRowCount);

        // Scaled by frame time, so a held key moves the slider at the same
        // rate on a 60Hz and a 240Hz display. VolumeStep is per second.
        float dt = Raylib.GetFrameTime();

        float delta = 0f;
        if (Raylib.IsKeyDown(KeyboardKey.Left)) delta = -VolumeStep * dt;
        else if (Raylib.IsKeyDown(KeyboardKey.Right)) delta = VolumeStep * dt;

        if (delta != 0f) AdjustVolumeRow(_selected, delta);

        if (Confirmed())
        {
            if (_selected == RowFullscreen) ToggleFullscreen();
            else if (_selected == RowBack) CloseSettings();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) CloseSettings();
    }

    private void Navigate(int rowCount)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.Tab))
            _selected = (_selected + 1) % rowCount;
        else if (Raylib.IsKeyPressed(KeyboardKey.Up))
            _selected = (_selected - 1 + rowCount) % rowCount;
    }

    private static bool Confirmed() =>
        Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);

    private void Activate(int index)
    {
        switch (MainOptions[index])
        {
            case "Play": EventRaised?.Invoke(GameEvent.PlayRequested); break;
            case "Test Arena": EventRaised?.Invoke(GameEvent.TestArenaRequested); break;
            case "Config Editor": EventRaised?.Invoke(GameEvent.AdminRequested); break;
            case "Settings": _page = Page.Settings; _selected = 0; break;
            case "Quit": EventRaised?.Invoke(GameEvent.QuitRequested); break;
        }
    }

    private void ToggleFullscreen() => Display.ToggleFullscreen(_settings);

    private void CloseSettings()
    {
        _settings.Save();
        _page = Page.Main;
        _selected = Array.IndexOf(MainOptions, "Settings");
        _draggingSlider = false;
    }

    private void AdjustVolumeRow(int row, float delta)
    {
        switch (row)
        {
            case RowMaster: _settings.MasterVolume = Clamp01(_settings.MasterVolume + delta); break;
            case RowMusic: _settings.MusicVolume = Clamp01(_settings.MusicVolume + delta); break;
            case RowSfx: _settings.SfxVolume = Clamp01(_settings.SfxVolume + delta); break;
            default: return;
        }

        _assets.ApplyVolumeSettings();
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);

    // --- Draw -----------------------------------------------------------

    public void Draw()
    {
        if (_page == Page.Main) DrawMain();
        else DrawSettings();
    }

    private void DrawMain()
    {
        MenuUi.CentredText("DOMAIN EXPANSION", CentreX, FirstRowY(MainOptions.Length) - 110, 42, MenuUi.Text);

        for (int i = 0; i < MainOptions.Length; i++)
        {
            Rectangle row = RowRect(i, MainOptions.Length);

            if (MenuUi.HoverSelects(row)) _selected = i;

            if (MenuUi.Button(row, MainOptions[i], _selected == i))
                Activate(i);
        }

        // Below the buttons, so it never competes with them for attention.
        int boardY = FirstRowY(MainOptions.Length) + MainOptions.Length * (RowHeight + RowGap) + 30;

        MenuUi.CentredText("TOP 5", CentreX, boardY, 18, MenuUi.TextDim);
        LeaderboardTable.Draw(_leaderboard, CentreX, boardY + 28);

        MenuUi.CentredText("Arrow keys or mouse - Enter or click to select",
            CentreX, Raylib.GetScreenHeight() - 48, 18, MenuUi.TextDim);
    }

    private void DrawSettings()
    {
        MenuUi.CentredText("SETTINGS", CentreX, FirstRowY(SettingsRowCount) - 110, 42, MenuUi.Text);

        // Hover selection first, so widgets draw with the correct highlight.
        // A slider drag keeps ownership of the selection until released.
        if (!_draggingSlider)
        {
            for (int i = 0; i < SettingsRowCount; i++)
            {
                if (MenuUi.HoverSelects(RowRect(i, SettingsRowCount))) _selected = i;
            }
        }

        DrawFullscreenRow();
        DrawVolumeRow(RowMaster, "Master Volume", _settings.MasterVolume, v => _settings.MasterVolume = v);
        DrawVolumeRow(RowMusic, "Music Volume", _settings.MusicVolume, v => _settings.MusicVolume = v);
        DrawVolumeRow(RowSfx, "SFX Volume", _settings.SfxVolume, v => _settings.SfxVolume = v);

        Rectangle back = RowRect(RowBack, SettingsRowCount);
        if (MenuUi.Button(back, "Back", _selected == RowBack)) CloseSettings();

        MenuUi.CentredText("Drag sliders or use Left/Right - Esc to go back",
            CentreX, Raylib.GetScreenHeight() - 48, 18, MenuUi.TextDim);
    }

    private void DrawFullscreenRow()
    {
        Rectangle row = RowRect(RowFullscreen, SettingsRowCount);
        bool selected = _selected == RowFullscreen;

        MenuUi.RowLabel(row, "Fullscreen", selected);

        if (MenuUi.Checkbox(ControlRect(RowFullscreen), _settings.IsFullScreen, selected))
            ToggleFullscreen();
    }

    private void DrawVolumeRow(int index, string label, float value, Action<float> setter)
    {
        Rectangle row = RowRect(index, SettingsRowCount);
        bool selected = _selected == index;

        MenuUi.RowLabel(row, label, selected);

        Rectangle control = ControlRect(index);

        // Reserve a fixed-width readout so the percentage never shifts the slider.
        var sliderRect = new Rectangle(control.X, control.Y, control.Width - 64f, control.Height);

        bool dragging = _draggingSlider && selected;
        float updated = MenuUi.Slider(sliderRect, value, selected, ref dragging);

        if (dragging) { _draggingSlider = true; _selected = index; }
        else if (_draggingSlider && selected) _draggingSlider = false;

        if (updated != value)
        {
            setter(updated);
            _assets.ApplyVolumeSettings();
        }

        int textY = (int)(row.Y + (row.Height - 20) / 2);
        Raylib.DrawText($"{updated * 100f:F0}%", (int)(control.X + control.Width - 52f), textY, 20,
            selected ? MenuUi.Accent : MenuUi.TextDim);
    }

    public void Unload()
    {
    }
}
