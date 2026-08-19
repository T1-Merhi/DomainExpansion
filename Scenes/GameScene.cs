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
    private HudRenderer _hud;

    // Death is raised from the tick loop, which can run several times per
    // frame; this makes sure the transition is requested exactly once.
    private bool _deathRaised;
    private float _accumulator;
    private int _stepsLastFrame;

    // Held across frames so a scroll during a frame that runs no tick
    // (accumulator below one step) is not silently dropped.
    private int _pendingWheel;

    public void Init(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;

        _world = new World(ScreenSize());
        _renderer = new WorldRenderer();
        _hud = new HudRenderer();
        _accumulator = 0f;
        _stepsLastFrame = 0;
        _deathRaised = false;
    }

    public void Update(float deltaTime)
    {
        HandleSceneInput();

        _world.Resize(ScreenSize());

        _pendingWheel += (int)Raylib.GetMouseWheelMove();

        _world.Input = ReadInput();
        _world.Input.WheelDelta = _pendingWheel;

        _accumulator += deltaTime;

        int steps = 0;
        while (_accumulator >= World.FixedStep && steps < MaxStepsPerFrame)
        {
            _world.Tick();
            _accumulator -= World.FixedStep;
            steps++;
        }

        // The sim zeroes WheelDelta once it consumes the scroll.
        if (steps > 0) _pendingWheel = _world.Input.WheelDelta;

        if (!_deathRaised && _world.Player.IsDead)
        {
            _deathRaised = true;
            EventRaised?.Invoke(GameEvent.PlayerDied);
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
        };
    }

    private void HandleSceneInput()
    {
        // Debug damage until enemies deal it for real (#19, #21).
        if (Raylib.IsKeyPressed(KeyboardKey.J)) _world.Player.TakeDamage(25f);
        if (Raylib.IsKeyPressed(KeyboardKey.H)) _world.Player.Heal(25f);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            EventRaised?.Invoke(GameEvent.MainMenuRequested);

        if (Raylib.IsKeyPressed(KeyboardKey.F3))
            _renderer.ShowDebug = !_renderer.ShowDebug;

        // Debug weapon swap on the active mount until #30 makes it a purchase.
        if (Raylib.IsKeyPressed(KeyboardKey.E)) CycleActiveWeapon();

        // Debug enemy spawning until #32 drives it from waves.
        if (Raylib.IsKeyPressed(KeyboardKey.Z)) _world.SpawnEnemy(EnemyType.Chaser, _world.RandomEdgePosition());
        if (Raylib.IsKeyPressed(KeyboardKey.X)) _world.SpawnEnemy(EnemyType.Shooter, _world.RandomEdgePosition());
        if (Raylib.IsKeyPressed(KeyboardKey.C)) _world.SpawnEnemy(EnemyType.Spawner, _world.RandomEdgePosition());

        // Debug shape switching until #29 makes it a purchase.
        for (int sides = 3; sides <= 8; sides++)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Zero + sides))
            {
                _world.Player.SideCount = sides;
                if (_world.Player.ActiveSide >= sides) _world.Player.ActiveSide = 0;

                // Geometry changed under it; easing from the old angle looks wrong.
                _world.Player.AimAt(_world.Input.MousePosition);
                _world.Player.SnapRotation();
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
        Raylib.DrawText("WASD move   LMB fire   wheel side   E weapon   3-8 shape", 20, 20, 18, Color.DarkGray);
        Raylib.DrawText("J damage   H heal   Z/X/C spawn enemy   ESC menu   F3 debug", 20, 42, 18, Color.DarkGray);

        _renderer.Draw(_world, _stepsLastFrame);
        _hud.Draw(_world);
    }

    public void Unload()
    {
    }
}
