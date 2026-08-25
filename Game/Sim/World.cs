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
    public readonly Shield Shield = new();

    /// <summary>Play area in world units. Currently matches the window.</summary>
    public Vector2 ArenaSize { get; private set; }

    /// <summary>Latest input snapshot, written by the scene before ticking.</summary>
    public InputState Input;

    public readonly WeaponCatalog Weapons = ConfigStore.Current.Weapons;
    public readonly EnemyCatalog EnemyDefs = ConfigStore.Current.Enemies;
    public readonly UpgradeCatalog UpgradeDefs = ConfigStore.Current.Upgrades;
    public readonly WaveGenerator Waves;
    public readonly WaveRunner WaveRunner;
    public readonly EnemyBehaviors Behaviors = new();

    public readonly Pool<Bullet> PlayerBullets = new(512);
    public readonly Pool<Enemy> Enemies = new(256);

    public readonly Pool<Explosion> Explosions = new(64);
    public readonly Pool<FloatingText> FloatingTexts = new(256);

    /// <summary>Spendable currency, earned by killing enemies.</summary>
    public int Coins { get; private set; }

    /// <summary>
    /// Leaderboard currency. Accumulates for the whole run and is never spent,
    /// so buying upgrades cannot reduce it - that separation is the entire
    /// reason it is not just Coins.
    /// </summary>
    public int Score { get; private set; }

    public void AddCoins(int amount)
    {
        if (amount > 0) Coins += amount;
    }

    /// <summary>Returns false and changes nothing when the player cannot afford it.</summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || Coins < amount) return false;

        Coins -= amount;
        return true;
    }

    /// <summary>
    /// Separate from PlayerBullets so the two never test against each other -
    /// player and enemy projectiles pass through one another by construction
    /// rather than by a filter check in the collision loop.
    /// </summary>
    public readonly Pool<Bullet> EnemyBullets = new(256);

    /// <summary>Packed RGBA for enemy fire, so it stays visually distinct from every weapon.</summary>
    private const uint EnemyBulletTint = 0x9B45C9FFu;

    public void SpawnEnemyBullet(Vector2 position, Vector2 velocity, float damage, float lifetime)
    {
        Bullet b = EnemyBullets.Rent();
        b.Position = position;
        b.Velocity = velocity;
        b.Damage = damage;
        b.Radius = Tuning.Player.EnemyBulletRadius;
        b.LifeTicks = Math.Max(1, (int)(lifetime * TickRate));
        b.ExplosionRadius = 0f;
        b.Tint = EnemyBulletTint;
    }

    private void StepEnemyBullets()
    {
        for (int i = EnemyBullets.ActiveCount - 1; i >= 0; i--)
        {
            Bullet b = EnemyBullets[i];
            b.Step();

            if (b.Expired(ArenaSize)) EnemyBullets.ReturnAt(i);
        }
    }

    private readonly SpatialGrid _grid = new();
    private readonly BulletGrid _enemyBulletGrid = new();

    /// <summary>Orange blast, used for chaser detonations and explosive rounds.</summary>
    private const uint BlastTint = 0xFF8C28FFu;

    /// <summary>Pale burst, used when an enemy dies from damage.</summary>
    private const uint DeathBurstTint = 0xFFE8D0FFu;

    public void AddExplosion(Vector2 position, float radius) =>
        AddExplosion(position, radius, BlastTint);

    public void AddExplosion(Vector2 position, float radius, uint tint)
    {
        Explosion e = Explosions.Rent();
        e.Position = position;
        e.Radius = radius;
        e.TicksLeft = Explosion.LifeTicks;
        e.Tint = tint;

        // Scaled by blast size so a grenade registers harder than a death pop.
        AddShake(radius * 0.05f);
    }

    /// <summary>
    /// Damage readout at the point of impact. Given a slight upward drift and a
    /// randomised horizontal nudge so repeated hits on one target fan out
    /// instead of stacking into an unreadable pile.
    /// </summary>
    public void AddFloatingText(Vector2 position, float amount, FloatingTextKind kind)
    {
        if (amount <= 0f) return;

        FloatingText t = FloatingTexts.Rent();
        t.Position = position;
        t.Velocity = new Vector2(NextFloat(-26f, 26f), -Tuning.Effects.FloatingTextRiseSpeed);
        t.Amount = amount;
        t.TicksLeft = FloatingText.LifeTicks;
        t.Kind = kind;
    }

    private void StepFloatingTexts()
    {
        for (int i = FloatingTexts.ActiveCount - 1; i >= 0; i--)
        {
            FloatingText t = FloatingTexts[i];
            t.Step();

            if (t.TicksLeft <= 0) FloatingTexts.ReturnAt(i);
        }
    }

    /// <summary>
    /// The only way to damage the player. God mode and the dead check live
    /// here rather than at each call site, so a new damage source cannot
    /// quietly opt out of them the way chaser detonation had.
    /// </summary>
    public void DamagePlayer(float amount)
    {
        // Dash invulnerability and god mode share this door, so a new damage
        // source cannot opt out of either.
        if (amount <= 0f || GodMode || Player.IsDead || Player.IsDashing) return;

        Player.TakeDamage(amount);
        AddShake(Tuning.Effects.ShakeOnPlayerHit);
    }

    /// <summary>Pale blue, so a shield absorb reads differently from a blast.</summary>
    private const uint ShieldHitTint = 0x7FC8FFFFu;

    /// <summary>
    /// Damage arriving from a direction. Absorbed by the arc covering that
    /// direction when one is intact, so explosions and detonations are stopped
    /// by the shield exactly as bullets are.
    /// </summary>
    public void DamagePlayerFrom(Vector2 source, float amount)
    {
        if (amount <= 0f) return;

        int arc = Shield.ArcCovering(Player.Position, source);

        if (arc >= 0)
        {
            Shield.DamageArc(arc, amount);
            AddExplosion(Player.Position + Vector2.Normalize(source - Player.Position) * Shield.Radius,
                18f, ShieldHitTint);
            return;
        }

        DamagePlayer(amount);
    }

    /// <summary>
    /// Keeps enemies outside an intact arc. Pushed along the surface normal
    /// rather than stopped dead, so a crowd cannot pin an arc and stall against
    /// it - they slide around toward a gap instead.
    /// </summary>
    private void PushEnemiesOffShield()
    {
        if (!Shield.Enabled || !Shield.AnyIntact) return;

        float barrier = Shield.Radius - Shield.Thickness * 0.5f;

        for (int i = 0; i < Enemies.ActiveCount; i++)
        {
            Enemy e = Enemies[i];

            Vector2 delta = e.Position - Player.Position;
            float distance = delta.Length();
            if (distance < 0.001f) continue;

            float minimum = barrier - e.Radius;
            if (distance >= minimum) continue;

            if (Shield.ArcCovering(Player.Position, e.Position) < 0) continue;

            e.Position = Player.Position + delta / distance * minimum;
        }
    }

    /// <summary>
    /// Applies damage and produces the feedback that goes with it, so no caller
    /// can damage an enemy without the player seeing why it died.
    /// </summary>
    private void DamageEnemy(Enemy enemy, float amount)
    {
        if (amount <= 0f || enemy.IsDead) return;

        enemy.TakeDamage(amount);
        RecordDamage(amount);
        AddFloatingText(enemy.Position, amount, FloatingTextKind.Damage);

        if (!enemy.IsDead) return;

        AddExplosion(enemy.Position, enemy.Radius * 1.6f, DeathBurstTint);
        AwardKill(enemy);
    }

    /// <summary>
    /// Paid only from DamageEnemy, so a chaser that consumes itself by
    /// detonating awards nothing - it sets PendingRemoval rather than dying
    /// from damage, and suiciding into the player must not fund the player.
    /// </summary>
    private void AwardKill(Enemy enemy)
    {
        int score = enemy.Stats.GetInt(StatId.ScoreValue);
        if (score > 0) Score += score;

        int coins = enemy.Stats.GetInt(StatId.CoinValue);
        if (coins <= 0) return;

        AddCoins(coins);

        // Offset upward so it does not sit under the damage number from the
        // killing blow, which spawns at the same instant and position.
        AddFloatingText(enemy.Position - new Vector2(0f, 18f), coins, FloatingTextKind.Coin);
    }

    private void StepExplosions()
    {
        for (int i = Explosions.ActiveCount - 1; i >= 0; i--)
        {
            Explosion e = Explosions[i];
            e.TicksLeft--;
            if (e.TicksLeft <= 0) Explosions.ReturnAt(i);
        }
    }

    /// <summary>
    /// Deterministic PRNG. Explicit rather than System.Random so spawn positions
    /// stay reproducible for a given seed under the fixed timestep.
    /// </summary>
    private uint _rng;

    /// <summary>
    /// Seeded per run, so runs differ. Kept as an explicit field rather than
    /// System.Random so a run stays reproducible from its seed - the test
    /// arena needs comparable repeats when balancing.
    /// </summary>
    public uint Seed { get; private set; }

    public void Reseed(uint seed)
    {
        // Zero is a fixed point for xorshift: it would return 0 forever.
        Seed = seed == 0 ? 0x9E3779B9u : seed;
        _rng = Seed;
    }

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

        Reseed((uint)Environment.TickCount);

        Waves = new WaveGenerator(ConfigStore.Current.Waves);
        WaveRunner = new WaveRunner(this, Waves);
    }

    public int WaveNumber => WaveRunner.WaveNumber;

    // --- Admin toggles ---------------------------------------------------
    // Inert in player mode: only the test arena ever sets them, and it is
    // unreachable outside admin mode.

    public bool GodMode;
    public bool InfiniteCoins;

    /// <summary>Multiplies real time before it reaches the accumulator, never the step itself.</summary>
    public float TimeScale = 1f;

    // --- Damage telemetry ------------------------------------------------

    private const int DpsWindowTicks = TickRate * 10;
    private readonly float[] _damageWindow = new float[DpsWindowTicks];
    private int _damageCursor;
    private float _damageInWindow;

    /// <summary>Damage dealt to enemies per second, averaged over the last ten seconds.</summary>
    public float RecentDps => _damageInWindow / (DpsWindowTicks / (float)TickRate);

    private void RecordDamage(float amount)
    {
        _damageWindow[_damageCursor] += amount;
        _damageInWindow += amount;
    }

    /// <summary>Advances the ring buffer, retiring the slot that just aged out.</summary>
    private void StepDamageWindow()
    {
        _damageCursor = (_damageCursor + 1) % DpsWindowTicks;

        _damageInWindow -= _damageWindow[_damageCursor];
        _damageWindow[_damageCursor] = 0f;

        if (_damageInWindow < 0f) _damageInWindow = 0f;
    }

    /// <summary>
    /// Screen shake, in world units of maximum offset. Decays every tick, and
    /// is capped so a heavy wave cannot make the arena unreadable.
    /// </summary>
    public float ShakeAmount { get; private set; }

    private static float ShakeCap => Tuning.Effects.ShakeCap;
    private static float ShakeDecayPerTick => Tuning.Effects.ShakeDecay;

    public void AddShake(float amount)
    {
        if (amount <= 0f) return;

        ShakeAmount = MathF.Min(ShakeCap, ShakeAmount + amount);
    }

    private void StepShake()
    {
        if (ShakeAmount <= 0.01f) { ShakeAmount = 0f; return; }

        ShakeAmount *= ShakeDecayPerTick;
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
        Player.TickHitFlash();
        Player.TickMuzzleFlash();

        // Consumed like the wheel: a press is an edge, and a frame running
        // several ticks must not spend it more than once.
        if (Input.DashPressed)
        {
            Player.TryDash(Input.MoveAxis, Input.MousePosition);
            Input.DashPressed = false;
        }

        Player.TickDash(ArenaSize);

        Shield.Tick();
        PushEnemiesOffShield();

        TickWeapons();
        StepBullets();
        StepEnemyBullets();
        TickEnemies();

        _grid.Rebuild(Enemies);
        ResolveBulletHits();
        ResolveBulletBlocks();
        ResolveEnemyBulletHits();
        ResolveEnemyContact();

        StepExplosions();
        StepFloatingTexts();
        StepShake();
        StepDamageWindow();
        SweepDeadEnemies();

        // Last, so a wave sees the arena state this tick produced rather than
        // the previous one - otherwise it declares itself clear a tick early.
        WaveRunner.Tick();
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
            else DamageEnemy(Enemies[hit], b.Damage);

            PlayerBullets.ReturnAt(i);
        }
    }

    /// <summary>
    /// Area damage falling off linearly to zero at the rim, with the blast
    /// visual. Spawning it here rather than at the call sites means every
    /// explosive impact is visible - previously the damage was applied with
    /// nothing drawn, so grenade rounds exploded invisibly.
    /// </summary>
    private void ApplyExplosion(Vector2 centre, float radius, float damage)
    {
        AddExplosion(centre, radius);

        _grid.QueryCircle(centre, radius, index =>
        {
            Enemy e = Enemies[index];
            if (e.IsDead) return;

            float distance = Vector2.Distance(centre, e.Position) - e.Radius;
            if (distance > radius) return;

            float falloff = 1f - MathF.Max(0f, distance) / radius;
            DamageEnemy(e, damage * falloff);
        });
    }

    /// <summary>
    /// Player fire shoots enemy projectiles out of the air. Both are consumed,
    /// so blocking costs a bullet and cannot clear a whole volley for free.
    ///
    /// Runs after ResolveBulletHits so a bullet that already hit an enemy is
    /// gone, and before ResolveEnemyBulletHits so anything blocked this tick
    /// never gets the chance to damage the player.
    /// </summary>
    private void ResolveBulletBlocks()
    {
        if (PlayerBullets.ActiveCount == 0 || EnemyBullets.ActiveCount == 0) return;

        // Indexed rather than compared pairwise: hundreds of player bullets
        // against hundreds of enemy bullets is a six-figure pair count every
        // tick, and this reduces it to the few sharing a cell.
        _enemyBulletGrid.Rebuild(EnemyBullets);

        for (int i = PlayerBullets.ActiveCount - 1; i >= 0; i--)
        {
            Bullet p = PlayerBullets[i];

            int hit = -1;
            _enemyBulletGrid.QueryCircle(p.Position, p.Radius, index =>
            {
                if (hit >= 0 || index >= EnemyBullets.ActiveCount) return;

                Bullet e = EnemyBullets[index];
                if (Collision.CirclesOverlap(p.Position, p.Radius, e.Position, e.Radius)) hit = index;
            });

            if (hit < 0) continue;

            // An explosive round still detonates when it blocks something.
            if (p.ExplosionRadius > 0f) ApplyExplosion(p.Position, p.ExplosionRadius, p.Damage);

            EnemyBullets.ReturnAt(hit);
            PlayerBullets.ReturnAt(i);

            // Returning swapped a different bullet into that slot, so the index
            // built above no longer describes the pool.
            _enemyBulletGrid.Rebuild(EnemyBullets);
        }
    }

    /// <summary>
    /// Enemy fire tests only against the player. No enemy pool lookup and no
    /// owner filtering - the two pools are separate, so anything surviving
    /// ResolveBulletBlocks is by definition still in flight.
    /// </summary>
    private void ResolveEnemyBulletHits()
    {
        if (Player.IsDead || GodMode) return;

        for (int i = EnemyBullets.ActiveCount - 1; i >= 0; i--)
        {
            Bullet b = EnemyBullets[i];

            // An intact arc eats the round before it reaches the player.
            if (Shield.Enabled && Shield.WithinBand(Player.Position, b.Position, b.Radius))
            {
                int arc = Shield.ArcCovering(Player.Position, b.Position);

                if (arc >= 0)
                {
                    Shield.DamageArc(arc, b.Damage);
                    AddExplosion(b.Position, 14f, ShieldHitTint);
                    EnemyBullets.ReturnAt(i);
                    continue;
                }
            }

            if (!Collision.CirclesOverlap(b.Position, b.Radius, Player.Position, Player.Radius)) continue;

            DamagePlayer(b.Damage);
            EnemyBullets.ReturnAt(i);
        }
    }

    private void ResolveEnemyContact()
    {
        if (Player.IsDead || GodMode) return;

        for (int i = 0; i < Enemies.ActiveCount; i++)
        {
            Enemy e = Enemies[i];
            if (e.IsDead) continue;

            // Driven by data rather than a type allowlist: an enemy that
            // detonates delivers its damage through the blast, and one with no
            // contact damage has nothing to apply. A new enemy type therefore
            // works without editing this line.
            if (e.Detonates) continue;

            float contact = e.Stats.Get(StatId.ContactDamage);
            if (contact <= 0f) continue;

            if (!Collision.CirclesOverlap(Player.Position, Player.Radius, e.Position, e.Radius)) continue;

            DamagePlayerFrom(e.Position, contact * FixedStep);
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
        e.Detonates = def.Detonates;
        e.Tint = def.PackedTint;
        e.PendingRemoval = false;

        // Start on cooldown rather than ready. At zero a shooter fires on the
        // tick it appears - offscreen, before the player can see it - and a
        // spawner emits with no telegraph having been drawn.
        e.ActionCooldown = InitialCooldownFor(e);

        // Scaling is layered on before health is read, so an enemy spawned
        // mid-wave by a spawner is as tough as one from the opening batch.
        Waves.ApplyScaling(e, Math.Max(1, WaveRunner.WaveNumber));

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

    /// <summary>
    /// One full action interval, so an enemy's first shot or spawn happens
    /// after it has been on screen for as long as its cadence implies.
    /// </summary>
    private static int InitialCooldownFor(Enemy enemy)
    {
        float rate = enemy.Stats.Get(StatId.FireRate);
        if (rate > 0f) return Math.Max(1, (int)MathF.Round(TickRate / rate));

        float interval = enemy.Stats.Get(StatId.SpawnInterval);
        if (interval > 0f) return Math.Max(1, (int)MathF.Round(interval * TickRate));

        return 0;
    }

    private void TickEnemies()
    {
        // Snapshot the count: a spawner adds enemies during this pass, and they
        // should not also be ticked on the tick they were created.
        int count = Enemies.ActiveCount;

        for (int i = 0; i < count; i++)
        {
            Enemy e = Enemies[i];
            Behaviors.For(e.Type).Tick(e, this);
            e.Position += e.Velocity * FixedStep;

            if (e.HitShakeTicks > 0) e.HitShakeTicks--;
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
        Player.MuzzleFlashTicks = Player.MuzzleFlashDuration;
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
            b.Radius = Tuning.Player.BulletRadius;
            b.LifeTicks = Math.Max(1, (int)(lifetime * TickRate));
            b.ExplosionRadius = explosion;
            b.Tint = weapon.Def.PackedTint;
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
