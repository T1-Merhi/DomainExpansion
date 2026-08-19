public class GameScene : IScene
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
        // Debug stand-in until #16 wires real player death.
        if (Raylib.IsKeyPressed(KeyboardKey.K))
            EventRaised?.Invoke(GameEvent.PlayerDied);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            EventRaised?.Invoke(GameEvent.MainMenuRequested);
    }

    public void Draw()
    {
        Raylib.DrawText("GAME", 20, 40, 30, Color.DarkBlue);
        Raylib.DrawText("K = simulate death (debug)", 20, 90, 20, Color.DarkGray);
        Raylib.DrawText("ESC = main menu", 20, 120, 20, Color.DarkGray);
    }

    public void Unload()
    {
    }
}
