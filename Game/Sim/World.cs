/// <summary>
/// The simulation. Advances in fixed steps and holds all game state.
/// Deliberately contains no Raylib drawing calls and takes no delta time:
/// one Tick is always exactly <see cref="TickRate"/> of a second.
/// </summary>
public sealed class World
{
    public const int TickRate = 60;
    public const float FixedStep = 1f / TickRate;

    public long TickCount { get; private set; }

    public readonly Player Player = new();

    /// <summary>Play area in world units. Currently matches the window.</summary>
    public Vector2 ArenaSize { get; private set; }

    /// <summary>Latest input snapshot, written by the scene before ticking.</summary>
    public InputState Input;

    public readonly WeaponCatalog Weapons = WeaponCatalog.Load();
    public readonly EnemyCatalog EnemyDefs = EnemyCatalog.Load();
    public readonly EnemyBehaviors Behaviors = new();

    public readonly Pool<Bullet> PlayerBullets = new(512);
    public readonly Pool<Enemy> Enemies = new(256);

    private readonly SpatialGrid _grid = new();

    /// <summary>
    /// Deterministic PRNG. Explicit rather than System.Random so spawn positions
    /// stay reproducible for a given seed under the fixed timestep.
    /// </summary>
    private uint _rng = 0x9E3779B9;

    public float NextFloat()
    {
        _rng ^= _rng << 13;
        _rng ^= _rng >> 17;
        _rng ^= _rng << 5;
        return (_rng & 0xFFFFFF) / (float)0x1000000;
    }

    public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

    public World(Vector2 arenaSize)
    {
        ArenaSize = arenaSize;
        Player.Position = arenaSize * 0.5f;

        // The triangle starts with one weapon on its primary side.
        Player.Mounts[0].Equip(Weapons.Find("rifle"));
    }

    public void Resize(Vector2 arenaSize) => ArenaSize = arenaSize;

    public void Tick()
    {
        TickCount++;

        Player.Move(Input.MoveAxis, ArenaSize);

        // Wheel is an edge, not a state: consume it so a frame that runs
        // several ticks does not apply the same scroll more than once.
        if (Input.WheelDelta != 0)
        {
            Player.CycleActiveSide(Input.WheelDelta);
            Input.WheelDelta = 0;
        }

        Player.AimAt(Input.MousePosition);
        Player.StepRotation();

        TickWeapons();
        StepBullets();
        TickEnemies();

        _grid.Rebuild(Enemies);
        ResolveBulletHits();
        ResolveEnemyContact();

        SweepDeadEnemies();
    }

    private void ResolveBulletHits()
    {
        for (int i = PlayerBullets.ActiveCount - 1; i >= 0; i--)
        {
            Bullet b = PlayerBullets[i];

            int hit = -1;
            _grid.QueryCircle(b.Position, b.Radius, index =>
            {
                if (hit >= 0) return;

                Enemy e = Enemies[index];
                if (e.IsDead) return;

                if (Collision.CirclesOverlap(b.Position, b.Radius, e.Position, e.Radius)) hit = index;
            });

            if (hit < 0) continue;

            if (b.ExplosionRadius > 0f) ApplyExplosion(b.Position, b.ExplosionRadius, b.Damage);
            else Enemies[hit].TakeDamage(b.Damage);

            PlayerBullets.ReturnAt(i);
        }
    }

    /// <summary>Area damage falling off linearly to zero at the rim.</summary>
    private void ApplyExplosion(Vector2 centre, float radius, float damage)
    {
        _grid.QueryCircle(centre, radius, index =>
        {
            Enemy e = Enemies[index];
            if (e.IsDead) return;

            float distance = Vector2.Distance(centre, e.Position) - e.Radius;
            if (distance > radius) return;

            float falloff = 1f - MathF.Max(0f, distance) / radius;
            e.TakeDamage(damage * falloff);
        });
    }

    private void ResolveEnemyContact()
    {
        if (Player.IsDead) return;

        for (int i = 0; i < Enemies.ActiveCount; i++)
        {
            Enemy e = Enemies[i];
            if (e.IsDead) continue;

            // Chasers deal their damage by detonating (#19), not by touching.
            if (e.Type != EnemyType.Shooter && e.Type != EnemyType.Spawner) continue;

            if (!Collision.CirclesOverlap(Player.Position, Player.Radius, e.Position, e.Radius)) continue;

            Player.TakeDamage(e.Stats.Get(StatId.ContactDamage) * FixedStep);
        }
    }

    private void SweepDeadEnemies()
    {
        for (int i = Enemies.ActiveCount - 1; i >= 0; i--)
        {
            Enemy e = Enemies[i];
            if (e.IsDead || e.PendingRemoval) Enemies.ReturnAt(i);
        }
    }

    /// <summary>Spawns one enemy of the given type at a world position.</summary>
    public Enemy SpawnEnemy(EnemyType type, Vector2 position)
    {
        EnemyDef def = EnemyDefs.Find(type);
        if (def == null) return null;

        Enemy e = Enemies.Rent();
        e.Type = type;
        e.Position = position;
        e.Velocity = Vector2.Zero;
        e.Radius = def.Radius;
        e.Stats = def.CreateStatBlock();
        e.Health = e.Stats.Get(StatId.MaxHealth);
        e.ActionCooldown = 0;
        e.PendingRemoval = false;
        return e;
    }

    /// <summary>A point just outside the arena edge, where waves arrive from.</summary>
    public Vector2 RandomEdgePosition(float margin = 40f)
    {
        float t = NextFloat();

        return (int)(NextFloat() * 4f) switch
        {
            0 => new Vector2(t * ArenaSize.X, -margin),
            1 => new Vector2(t * ArenaSize.X, ArenaSize.Y + margin),
            2 => new Vector2(-margin, t * ArenaSize.Y),
            _ => new Vector2(ArenaSize.X + margin, t * ArenaSize.Y),
        };
    }

    private void TickEnemies()
    {
        for (int i = 0; i < Enemies.ActiveCount; i++)
        {
            Enemy e = Enemies[i];
            Behaviors.For(e.Type).Tick(e, this);
            e.Position += e.Velocity * FixedStep;
        }

        // Removal is deferred to SweepDeadEnemies, after collision has run.
    }

    private void TickWeapons()
    {
        for (int i = 0; i < Player.SideCount; i++)
        {
            Player.Mounts[i].Weapon?.TickCooldown();
        }

        if (!Input.FireHeld) return;

        Mount mount = Player.ActiveMount;
        if (mount.IsEmpty || !mount.Weapon.IsReady) return;

        FireMount(Player.ActiveSide, mount.Weapon);
        mount.Weapon.StartCooldown();
    }

    private void FireMount(int side, WeaponInstance weapon)
    {
        Vector2 origin = Player.SideMidpoint(side);
        float aim = Player.SideNormalAngle(side);

        int count = Math.Max(1, weapon.Stats.GetInt(StatId.ProjectileCount));
        float spreadDeg = weapon.Stats.Get(StatId.Spread);
        float speed = weapon.Stats.Get(StatId.BulletSpeed);
        float damage = weapon.Stats.Get(StatId.Damage);
        float lifetime = weapon.Stats.Get(StatId.BulletLifetime);
        float explosion = weapon.Def.OnHit == OnHit.Explode
            ? weapon.Stats.Get(StatId.ExplosionRadius)
            : 0f;

        for (int i = 0; i < count; i++)
        {
            // Fan the pellets evenly across the spread arc rather than
            // randomly, so a shotgun pattern is readable and consistent.
            float offset = count == 1
                ? 0f
                : (i / (float)(count - 1) - 0.5f) * spreadDeg * MathF.PI / 180f;

            float angle = aim + offset;

            Bullet b = PlayerBullets.Rent();
            b.Position = origin;
            b.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
            b.Damage = damage;
            b.Radius = 4f;
            b.LifeTicks = Math.Max(1, (int)(lifetime * TickRate));
            b.ExplosionRadius = explosion;
        }
    }

    private void StepBullets()
    {
        // Backwards: ReturnAt swaps the last active item into the freed slot.
        for (int i = PlayerBullets.ActiveCount - 1; i >= 0; i--)
        {
            Bullet b = PlayerBullets[i];
            b.Step();

            if (b.Expired(ArenaSize)) PlayerBullets.ReturnAt(i);
        }
    }
}
