/// <summary>
/// Admin sandbox: exercise balance without playing a run.
///
/// Wraps the same World and fixed-timestep loop as GameScene, so what is
/// measured here is the real simulation rather than an approximation of it.
/// </summary>
public class TestArenaScene : IScene
{
    public event Action<GameEvent> EventRaised;

    private const int MaxStepsPerFrame = 5;

    private static readonly Color Ink = new(45, 45, 55, 255);
    private static readonly Color Muted = new(130, 130, 142, 255);
    private static readonly Color Accent = new(56, 118, 200, 255);
    private static readonly Color On = new(56, 158, 74, 255);

    private AssetManager _assets;
    private GameSettings _settings;

    private World _world;
    private WorldRenderer _renderer;
    private HudRenderer _hud;
    private Shop _shop;

    private float _accumulator;
    private int _spawnCount = 5;
    private int _waveTarget = 1;

    public void Init(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;

        _world = new World(new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
        _renderer = new WorldRenderer();
        _hud = new HudRenderer();
        _shop = new Shop(_world);
        _accumulator = 0f;
    }

    public void Update(float deltaTime)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            EventRaised?.Invoke(GameEvent.MainMenuRequested);
            return;
        }

        _world.Resize(new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
        _world.Input = ReadInput();

        if (_world.GodMode) _world.Player.HealFull();
        if (_world.InfiniteCoins) _world.AddCoins(1000);

        // Time scale multiplies the frame's real time before it reaches the
        // accumulator, so the simulation slows or speeds while every tick stays
        // exactly 1/60s - the sim never sees a variable step.
        _accumulator += deltaTime * _world.TimeScale;

        int steps = 0;
        while (_accumulator >= World.FixedStep && steps < MaxStepsPerFrame)
        {
            _world.Tick();
            _accumulator -= World.FixedStep;
            steps++;
        }

        if (steps == MaxStepsPerFrame) _accumulator = 0f;
    }

    private static InputState ReadInput()
    {
        var axis = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W)) axis.Y -= 1f;
        if (Raylib.IsKeyDown(KeyboardKey.S)) axis.Y += 1f;
        if (Raylib.IsKeyDown(KeyboardKey.A)) axis.X -= 1f;
        if (Raylib.IsKeyDown(KeyboardKey.D)) axis.X += 1f;
        if (axis.LengthSquared() > 1f) axis = Vector2.Normalize(axis);

        return new InputState
        {
            MousePosition = Raylib.GetMousePosition(),
            MoveAxis = axis,
            FireHeld = Raylib.IsMouseButtonDown(MouseButton.Left),
            WheelDelta = 0,
        };
    }

    public void Draw()
    {
        _renderer.Draw(_world, 0);
        _hud.Draw(_world);

        DrawControlPanel();
        DrawReadout();
    }

    // --- Controls ---------------------------------------------------------

    private void DrawControlPanel()
    {
        var panel = new Rectangle(16, 80, 250, 400);
        Raylib.DrawRectangleRec(panel, new Color(246, 246, 250, 235));
        Raylib.DrawRectangleLinesEx(panel, 2f, new Color(70, 70, 82, 255));

        int x = (int)panel.X + 14;
        int y = (int)panel.Y + 12;

        Raylib.DrawText("TEST ARENA", x, y, 20, Ink);
        y += 32;

        y = DrawStepper("Spawn count", ref _spawnCount, 1, 200, x, y);

        y = DrawAction($"Spawn {_spawnCount} chasers", x, y,
            () => SpawnMany(EnemyType.Chaser, _spawnCount));
        y = DrawAction($"Spawn {_spawnCount} shooters", x, y,
            () => SpawnMany(EnemyType.Shooter, _spawnCount));
        y = DrawAction($"Spawn {_spawnCount} spawners", x, y,
            () => SpawnMany(EnemyType.Spawner, _spawnCount));

        y = DrawAction("Kill all enemies", x, y, KillAll);
        y += 6;

        y = DrawStepper("Wave", ref _waveTarget, 1, 999, x, y);
        y = DrawAction($"Jump to wave {_waveTarget}", x, y,
            () => _world.WaveRunner.StartWave(_waveTarget));
        y += 6;

        y = DrawToggle("God mode", _world.GodMode, x, y, () => _world.GodMode = !_world.GodMode);
        y = DrawToggle("Infinite coins", _world.InfiniteCoins, x, y,
            () => _world.InfiniteCoins = !_world.InfiniteCoins);

        y = DrawAction("Grant all upgrades", x, y, GrantAllUpgrades);
        y += 6;

        DrawTimeScale(x, y);
    }

    private int DrawStepper(string label, ref int value, int min, int max, int x, int y)
    {
        Raylib.DrawText($"{label}: {value}", x, y + 4, 16, Ink);

        var minus = new Rectangle(x + 170, y, 22, 22);
        var plus = new Rectangle(x + 196, y, 22, 22);

        if (DrawTinyButton(minus, "-")) value = Math.Max(min, value - StepFor(value));
        if (DrawTinyButton(plus, "+")) value = Math.Min(max, value + StepFor(value));

        return y + 28;
    }

    private static int StepFor(int value) => value >= 100 ? 25 : value >= 20 ? 5 : 1;

    private bool DrawTinyButton(Rectangle rect, string label)
    {
        bool hovered = MenuUi.IsHovered(rect);
        Raylib.DrawRectangleRec(rect, hovered ? new Color(228, 236, 248, 255) : new Color(238, 238, 244, 255));
        Raylib.DrawRectangleLinesEx(rect, 1.2f, hovered ? Accent : new Color(212, 212, 220, 255));

        int w = Raylib.MeasureText(label, 16);
        Raylib.DrawText(label, (int)(rect.X + rect.Width / 2) - w / 2, (int)rect.Y + 3, 16, Ink);

        return MenuUi.Clicked(rect);
    }

    private int DrawAction(string label, int x, int y, Action onClick)
    {
        var rect = new Rectangle(x, y, 218, 26);
        bool hovered = MenuUi.IsHovered(rect);

        Raylib.DrawRectangleRec(rect, hovered ? new Color(228, 236, 248, 255) : new Color(238, 238, 244, 255));
        Raylib.DrawRectangleLinesEx(rect, 1.2f, hovered ? Accent : new Color(212, 212, 220, 255));
        Raylib.DrawText(label, x + 9, y + 5, 15, Ink);

        if (MenuUi.Clicked(rect)) onClick();

        return y + 30;
    }

    private int DrawToggle(string label, bool value, int x, int y, Action onClick)
    {
        var rect = new Rectangle(x, y, 218, 26);
        bool hovered = MenuUi.IsHovered(rect);

        Raylib.DrawRectangleRec(rect, hovered ? new Color(228, 236, 248, 255) : new Color(238, 238, 244, 255));
        Raylib.DrawRectangleLinesEx(rect, 1.2f, value ? On : new Color(212, 212, 220, 255));

        Raylib.DrawText(label, x + 9, y + 5, 15, Ink);

        string state = value ? "ON" : "OFF";
        int w = Raylib.MeasureText(state, 15);
        Raylib.DrawText(state, x + 209 - w, y + 5, 15, value ? On : Muted);

        if (MenuUi.Clicked(rect)) onClick();

        return y + 30;
    }

    private void DrawTimeScale(int x, int y)
    {
        Raylib.DrawText($"Time scale: {_world.TimeScale:0.00}x", x, y, 16, Ink);

        var track = new Rectangle(x, y + 24, 218, 6);
        Raylib.DrawRectangleRounded(track, 1f, 4, new Color(216, 216, 224, 255));

        const float max = 3f;
        float t = Math.Clamp(_world.TimeScale / max, 0f, 1f);
        Raylib.DrawRectangleRounded(new Rectangle(x, y + 24, 218 * t, 6), 1f, 4, Accent);

        var hit = new Rectangle(x, y + 16, 218, 22);
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && MenuUi.IsHovered(hit))
        {
            float ratio = (Raylib.GetMousePosition().X - x) / 218f;
            _world.TimeScale = MathF.Max(0f, Math.Clamp(ratio, 0f, 1f) * max);
        }

        Raylib.DrawCircleV(new Vector2(x + 218 * t, y + 27), 7f, Accent);
    }

    // --- Actions ----------------------------------------------------------

    private void SpawnMany(EnemyType type, int count)
    {
        for (int i = 0; i < count; i++) _world.SpawnEnemy(type, _world.RandomEdgePosition());
    }

    private void KillAll()
    {
        for (int i = _world.Enemies.ActiveCount - 1; i >= 0; i--) _world.Enemies.ReturnAt(i);
    }

    /// <summary>Levels every mount and player upgrade to its cap, ignoring cost.</summary>
    private void GrantAllUpgrades()
    {
        foreach (UpgradeDef def in _world.UpgradeDefs.Upgrades)
        {
            if (def.Kind != UpgradeKind.MountStat && def.Kind != UpgradeKind.PlayerStat) continue;

            int cap = def.MaxLevel > 0 ? def.MaxLevel : 5;

            for (int side = 0; side < _world.Player.SideCount; side++)
            {
                for (int level = _shop.LevelOf(def, side); level < cap; level++)
                    _shop.GrantFree(def, side);

                if (def.Kind == UpgradeKind.PlayerStat) break;
            }
        }
    }

    // --- Readout ----------------------------------------------------------

    private void DrawReadout()
    {
        var panel = new Rectangle(Raylib.GetScreenWidth() - 282, 80, 266, 260);
        Raylib.DrawRectangleRec(panel, new Color(246, 246, 250, 235));
        Raylib.DrawRectangleLinesEx(panel, 2f, new Color(70, 70, 82, 255));

        int x = (int)panel.X + 14;
        int y = (int)panel.Y + 12;

        Raylib.DrawText("LIVE", x, y, 20, Ink);
        y += 30;

        Player p = _world.Player;
        Mount mount = p.ActiveMount;

        y = Line(x, y, "DPS (10s)", $"{_world.RecentDps:0.0}");
        y = Line(x, y, "Enemies", $"{_world.Enemies.ActiveCount}");
        y = Line(x, y, "  chaser", $"{CountOf(EnemyType.Chaser)}");
        y = Line(x, y, "  shooter", $"{CountOf(EnemyType.Shooter)}");
        y = Line(x, y, "  spawner", $"{CountOf(EnemyType.Spawner)}");
        y += 8;

        y = Line(x, y, "Health", $"{p.Health:0} / {p.MaxHealth:0}");
        y = Line(x, y, "Move speed", $"{p.Stats.Get(StatId.MoveSpeed):0}");

        if (!mount.IsEmpty)
        {
            StatBlock s = mount.Weapon.Stats;
            y = Line(x, y, "Damage", $"{s.Get(StatId.Damage):0.#}");
            y = Line(x, y, "Fire rate", $"{s.Get(StatId.FireRate):0.##}/s");
            y = Line(x, y, "Shots", $"{s.GetInt(StatId.ProjectileCount)}");
        }

        Raylib.DrawText($"config v{ConfigStore.Current.Version}", x, y + 6, 14, Muted);
    }

    private int CountOf(EnemyType type)
    {
        int n = 0;
        for (int i = 0; i < _world.Enemies.ActiveCount; i++)
            if (_world.Enemies[i].Type == type) n++;

        return n;
    }

    private static int Line(int x, int y, string label, string value)
    {
        Raylib.DrawText(label, x, y, 15, Muted);

        int w = Raylib.MeasureText(value, 15);
        Raylib.DrawText(value, x + 238 - w, y, 15, Ink);

        return y + 20;
    }

    public void Unload()
    {
    }
}
