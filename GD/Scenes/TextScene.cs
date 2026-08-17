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

