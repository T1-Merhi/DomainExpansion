public class GameScene : IScene
{
    public event Action<GameEvent> EventRaised;

    // Above this, we drop the backlog rather than spiral trying to catch up
    // after a long stall (window drag, breakpoint, alt-tab).
    private const int MaxStepsPerFrame = 5;

    private AssetManager _assets;
    private GameSettings _settings;

    private World _world;
    private WorldRenderer _renderer;
    private float _accumulator;
    private int _stepsLastFrame;

    public void Init(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;

        var centre = new Vector2(Raylib.GetScreenWidth() * 0.5f, Raylib.GetScreenHeight() * 0.5f);
        _world = new World(centre);
        _renderer = new WorldRenderer();
        _accumulator = 0f;
        _stepsLastFrame = 0;
    }

    public void Update(float deltaTime)
    {
        HandleSceneInput();

        _world.Input = ReadInput();

        _accumulator += deltaTime;

        int steps = 0;
        while (_accumulator >= World.FixedStep && steps < MaxStepsPerFrame)
        {
            _world.Tick();
            _accumulator -= World.FixedStep;
            steps++;
        }

        // Hit the clamp: discard the leftover time so the next frame starts clean.
        if (steps == MaxStepsPerFrame) _accumulator = 0f;

        _stepsLastFrame = steps;
    }

    private static InputState ReadInput()
    {
        return new InputState
        {
            MousePosition = Raylib.GetMousePosition(),
            FireHeld = Raylib.IsMouseButtonDown(MouseButton.Left),
            WheelDelta = (int)Raylib.GetMouseWheelMove(),
        };
    }

    private void HandleSceneInput()
    {
        // Debug stand-in until #16 wires real player death.
        if (Raylib.IsKeyPressed(KeyboardKey.K))
            EventRaised?.Invoke(GameEvent.PlayerDied);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            EventRaised?.Invoke(GameEvent.MainMenuRequested);

        if (Raylib.IsKeyPressed(KeyboardKey.F3))
            _renderer.ShowDebug = !_renderer.ShowDebug;

        // Debug shape switching until #29 makes it a purchase.
        for (int sides = 3; sides <= 8; sides++)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Zero + sides))
            {
                _world.Player.SideCount = sides;
                if (_world.Player.ActiveSide >= sides) _world.Player.ActiveSide = 0;
            }
        }
    }

    public void Draw()
    {
        Raylib.DrawText("GAME", 20, 30, 26, Color.DarkBlue);
        Raylib.DrawText("3-8 = shape   K = die   ESC = menu   F3 = debug", 20, 66, 18, Color.DarkGray);

        _renderer.Draw(_world, _stepsLastFrame);
    }

    public void Unload()
    {
    }
}
