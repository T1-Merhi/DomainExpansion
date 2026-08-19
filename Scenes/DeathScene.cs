public class DeathScene : IScene
{
    public event Action<GameEvent> EventRaised;

    private AssetManager _assets;
    private GameSettings _settings;

    public void Init(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;
    }

    public void Update(float deltaTime)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
            EventRaised?.Invoke(GameEvent.RestartRequested);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            EventRaised?.Invoke(GameEvent.MainMenuRequested);
    }

    public void Draw()
    {
        // #38 replaces this with the full summary and leaderboard.
        int centreX = Raylib.GetScreenWidth() / 2;
        int y = Raylib.GetScreenHeight() / 2 - 120;

        MenuUi.CentredText("YOU DIED", centreX, y, 46, Color.Maroon);

        MenuUi.CentredText($"Score  {RunResult.Score}", centreX, y + 90, 28, MenuUi.Text);
        MenuUi.CentredText($"Reached wave {RunResult.Wave}", centreX, y + 128, 22, MenuUi.TextDim);
        MenuUi.CentredText($"Coins earned  {RunResult.Coins}", centreX, y + 158, 20, MenuUi.TextDim);

        MenuUi.CentredText("SPACE = restart      ESC = main menu",
            centreX, Raylib.GetScreenHeight() - 60, 18, MenuUi.TextDim);
    }

    public void Unload()
    {
    }
}
