public class GameScene : IScene
{
    public event Action<GameEvent> EventRaised;

    // Above this, we drop the backlog rather than spiral trying to catch up
    // after a long stall (window drag, breakpoint, alt-tab).
    private const int MaxStepsPerFrame = 5;

    /// <summary>
    /// Large enough that a single press affords the more expensive upgrades,
    /// so the shop can be exercised without grinding kills.
    /// </summary>
    private const int DebugCoinGrant = 99999;

    private AssetManager _assets;
    private GameSettings _settings;

    private World _world;
    private WorldRenderer _renderer;
    private HudRenderer _hud;
    private ShopRenderer _shopUi;
    private Shop _shop;
    private PauseRenderer _pauseUi;

    private bool _shopOpen;
    private bool _paused;

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

        // Re-read config from the shared folder on every run start, so an
        // admin instance's saved edits take effect on restart with no relaunch.
        ConfigStore.Current.Reload();

        // Stale values from the previous run would otherwise show on the death
        // screen if this run ends before RunResult.Capture is reached.
        RunResult.Clear();

        _world = new World(ScreenSize());
        _renderer = new WorldRenderer();
        _hud = new HudRenderer();
        _shopUi = new ShopRenderer();
        _shop = new Shop(_world);
        _pauseUi = new PauseRenderer();
        _accumulator = 0f;
        _stepsLastFrame = 0;
        _deathRaised = false;
        _shopOpen = false;
        _paused = false;
    }

    public void Update(float deltaTime)
    {
        bool overlayConsumedInput = HandleOverlayInput();

        // Both overlays freeze the simulation. The accumulator is deliberately
        // not advanced, so closing one cannot release a burst of banked ticks.
        if (_shopOpen || _paused || overlayConsumedInput)
        {
            // Overlay interaction happens here, not in Draw. Acting during the
            // draw pass meant a scene transition could be raised while the
            // frame was still being rendered.
            if (!overlayConsumedInput)
            {
                if (_shopOpen) _shopUi.HandleInput(_world, _shop);
                else if (_paused) HandlePauseMenu();
            }

            _stepsLastFrame = 0;
            return;
        }

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
            RunResult.Capture(_world);
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
            DashPressed = Raylib.IsKeyPressed(KeyboardKey.LeftShift) ||
                          Raylib.IsKeyPressed(KeyboardKey.RightShift),
        };
    }

    /// <summary>
    /// The two overlays own separate inputs so they can never fight over one
    /// key: right-click is the only thing that opens or closes the shop, and
    /// ESC is the only thing that opens or closes the pause menu.
    ///
    /// Returns true when this frame's input was spent here, so Update can stop.
    /// IsKeyPressed stays true for a whole frame, so without that a single ESC
    /// would be read by more than one handler.
    /// </summary>
    private bool HandleOverlayInput()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            _shopOpen = !_shopOpen;

            // Never both at once.
            if (_shopOpen) _paused = false;
            return true;
        }

        // The shop ignores ESC entirely - it closes with right-click only, so
        // pressing ESC repeatedly can never walk out of the run.
        if (_shopOpen) return false;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            _paused = !_paused;
            if (_paused) _pauseUi.Reset();
            return true;
        }

        return false;
    }

    private void HandleSceneInput()
    {
        // A real player feature - the HUD advertises it - so it stays ungated.
        if (Raylib.IsKeyPressed(KeyboardKey.Enter)) _world.WaveRunner.SkipRest();

        // Everything below is a cheat. Ungated it shipped a one-key coin
        // exploit and free upgrades in the player build, which would make the
        // leaderboard meaningless.
        if (!AppMode.IsAdmin) return;

        // Through the guarded path, so god mode protects against the debug key
        // too - one exception is how the rule drifts.
        if (Raylib.IsKeyPressed(KeyboardKey.J)) _world.DamagePlayer(25f);
        if (Raylib.IsKeyPressed(KeyboardKey.H)) _world.Player.Heal(25f);
        if (Raylib.IsKeyPressed(KeyboardKey.G)) _world.AddCoins(DebugCoinGrant);

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
        if (AppMode.IsAdmin)
        {
            Raylib.DrawText("WASD move   SHIFT dash   LMB fire   RMB shop   wheel side   E weapon", 20, 20, 18, Color.DarkGray);
            Raylib.DrawText("J damage   H heal   G coins   Z/X/C spawn   ENTER skip rest   ESC pause   F3 debug",
                20, 42, 18, Color.DarkGray);
        }

        _renderer.Draw(_world, _stepsLastFrame);
        _hud.Draw(_world);

        if (_shopOpen) _shopUi.Draw(_world, _shop);
        else if (_paused) _pauseUi.Draw();
    }

    /// <summary>
    /// Leaving the run is only reachable through the pause menu now, so it
    /// takes a deliberate choice rather than a stray key press.
    /// </summary>
    private void HandlePauseMenu()
    {
        switch (_pauseUi.HandleInput())
        {
            case PauseAction.Resume:
                _paused = false;
                break;

            case PauseAction.Quit:
                _paused = false;
                EventRaised?.Invoke(GameEvent.MainMenuRequested);
                break;
        }
    }

    public void Unload()
    {
    }
}
