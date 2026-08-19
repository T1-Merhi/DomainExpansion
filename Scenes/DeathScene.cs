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
        // #38 replaces this with the score summary and leaderboard.
        Raylib.DrawText("YOU DIED", 20, 40, 40, Color.Maroon);
        Raylib.DrawText("SPACE = restart", 20, 110, 20, Color.DarkGray);
        Raylib.DrawText("ESC = main menu", 20, 140, 20, Color.DarkGray);
    }

    public void Unload()
    {
    }
}
