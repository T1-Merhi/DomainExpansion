public class DeathScene : IScene
{
    public event Action<GameEvent> EventRaised;

    private const int MaxNameLength = 12;

    private AssetManager _assets;
    private GameSettings _settings;

    private Leaderboard _leaderboard;
    private bool _enteringName;
    private string _name = "";
    private int _newEntryIndex = -1;

    public void Init(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;

        _leaderboard = Leaderboard.Load();

        // Only prompt when the run actually earns a place, so an ordinary run
        // goes straight to the summary instead of asking for a name pointlessly.
        _enteringName = _leaderboard.Qualifies(RunResult.Score);
        _name = "";
        _newEntryIndex = -1;
    }

    public void Update(float deltaTime)
    {
        if (_enteringName) UpdateNameEntry();
        else UpdateSummary();
    }

    private void UpdateNameEntry()
    {
        // GetCharPressed yields the typed characters, so layout and modifiers
        // are handled by the platform rather than mapped from key codes.
        int codepoint = Raylib.GetCharPressed();
        while (codepoint > 0)
        {
            if (codepoint >= 32 && codepoint <= 125 && _name.Length < MaxNameLength)
                _name += (char)codepoint;

            codepoint = Raylib.GetCharPressed();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _name.Length > 0)
            _name = _name.Substring(0, _name.Length - 1);

        if (Raylib.IsKeyPressed(KeyboardKey.Enter)) Commit();
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Commit();
    }

    private void Commit()
    {
        _newEntryIndex = _leaderboard.Insert(_name, RunResult.Score, RunResult.Wave);
        _leaderboard.Save();
        _enteringName = false;
    }

    private void UpdateSummary()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
            EventRaised?.Invoke(GameEvent.RestartRequested);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            EventRaised?.Invoke(GameEvent.MainMenuRequested);
    }

    public void Draw()
    {
        int centreX = Raylib.GetScreenWidth() / 2;
        int top = Raylib.GetScreenHeight() / 2 - 210;

        MenuUi.CentredText("YOU DIED", centreX, top, 44, Color.Maroon);

        MenuUi.CentredText($"Score {RunResult.Score}    Wave {RunResult.Wave}    Coins {RunResult.Coins}",
            centreX, top + 60, 20, MenuUi.TextDim);

        if (_enteringName) DrawNameEntry(centreX, top + 110);
        else DrawLeaderboard(centreX, top + 110);
    }

    private void DrawNameEntry(int centreX, int y)
    {
        MenuUi.CentredText("NEW HIGH SCORE", centreX, y, 26, MenuUi.Accent);
        MenuUi.CentredText("Enter your name", centreX, y + 36, 17, MenuUi.TextDim);

        var box = new Rectangle(centreX - 150, y + 66, 300, 44);
        Raylib.DrawRectangleRec(box, new Color(240, 240, 246, 255));
        Raylib.DrawRectangleLinesEx(box, 2f, MenuUi.Accent);

        // Blinking caret, so an empty field still reads as awaiting input.
        bool caretOn = (int)(Raylib.GetTime() * 2) % 2 == 0;
        string shown = _name + (caretOn ? "_" : " ");

        MenuUi.CentredText(shown, centreX, (int)box.Y + 11, 24, MenuUi.Text);
        MenuUi.CentredText("ENTER to confirm", centreX, y + 124, 16, MenuUi.TextDim);
    }

    private void DrawLeaderboard(int centreX, int y)
    {
        MenuUi.CentredText("TOP 5", centreX, y, 22, MenuUi.Text);

        LeaderboardTable.Draw(_leaderboard, centreX, y + 36, _newEntryIndex);

        MenuUi.CentredText("SPACE = restart      ESC = main menu",
            centreX, Raylib.GetScreenHeight() - 56, 18, MenuUi.TextDim);
    }

    public void Unload()
    {
    }
}
