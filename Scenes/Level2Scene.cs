public class Level2Scene : IScene
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
            EventRaised?.Invoke(GameEvent.Level2Completed);
    }

    public void Draw()
    {
        Raylib.DrawText("LEVEL 2", 20, 40, 30, Color.DarkBlue);
        Raylib.DrawText("Press SPACE to complete the level", 20, 90, 20, Color.DarkGray);
    }

    public void Unload()
    {
    }
}
