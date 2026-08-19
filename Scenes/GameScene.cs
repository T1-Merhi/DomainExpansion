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

        _world = new World(ScreenSize());
        _renderer = new WorldRenderer();
        _accumulator = 0f;
        _stepsLastFrame = 0;
    }

    public void Update(float deltaTime)
    {
        HandleSceneInput();

        _world.Resize(ScreenSize());
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

    private static Vector2 ScreenSize() =>
        new(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

    private static InputState ReadInput()
    {
        var axis = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) axis.Y -= 1f;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) axis.Y += 1f;
        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) axis.X -= 1f;
        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) axis.X += 1f;

        // Normalise so diagonals are not faster than cardinals.
        if (axis.LengthSquared() > 1f) axis = Vector2.Normalize(axis);

        return new InputState
        {
            MousePosition = Raylib.GetMousePosition(),
            MoveAxis = axis,
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

        // Debug weapon swap on the active mount until #30 makes it a purchase.
        if (Raylib.IsKeyPressed(KeyboardKey.E)) CycleActiveWeapon();

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

    private void CycleActiveWeapon()
    {
        var defs = _world.Weapons.Weapons;
        if (defs.Count == 0) return;

        Mount mount = _world.Player.ActiveMount;

        int next = 0;
        if (!mount.IsEmpty)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].Id == mount.Weapon.Def.Id) { next = (i + 1) % defs.Count; break; }
            }
        }

        mount.Equip(defs[next]);
    }

    public void Draw()
    {
        Raylib.DrawText("GAME", 20, 30, 26, Color.DarkBlue);
        Raylib.DrawText("WASD move   LMB fire   E weapon   3-8 shape   K die   ESC menu   F3 debug",
            20, 66, 18, Color.DarkGray);

        Mount active = _world.Player.ActiveMount;
        Raylib.DrawText(active.IsEmpty ? "(empty mount)" : active.Weapon.Name,
            20, 92, 20, active.IsEmpty ? Color.Gray : Color.Orange);

        _renderer.Draw(_world, _stepsLastFrame);
    }

    public void Unload()
    {
    }
}
