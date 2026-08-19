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
    public readonly Pool<Bullet> PlayerBullets = new(512);

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
        Player.AimAt(Input.MousePosition);

        TickWeapons();
        StepBullets();
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
