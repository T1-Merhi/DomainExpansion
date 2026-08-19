/// <summary>
/// Per-type behaviour. Implementations are stateless and shared across every
/// enemy of that type - all mutable state lives on the pooled Enemy, which is
/// what keeps enemies cheap to pool.
/// </summary>
public interface IEnemyBehavior
{
    void Tick(Enemy enemy, World world);
}

/// <summary>Type-to-behaviour lookup, resolved once at construction.</summary>
public sealed class EnemyBehaviors
{
    private readonly IEnemyBehavior[] _byType;

    public EnemyBehaviors()
    {
        _byType = new IEnemyBehavior[Enum.GetValues<EnemyType>().Length];
        _byType[(int)EnemyType.Chaser] = new ChaserBehavior();
        _byType[(int)EnemyType.Shooter] = new ShooterBehavior();
        _byType[(int)EnemyType.Spawner] = new SpawnerBehavior();
    }

    public IEnemyBehavior For(EnemyType type) => _byType[(int)type];
}

/// <summary>
/// Red square. Steers straight at the player and detonates once inside its
/// proximity radius, dealing area damage. Removes itself on detonation.
/// </summary>
public sealed class ChaserBehavior : IEnemyBehavior
{
    public void Tick(Enemy enemy, World world)
    {
        Player player = world.Player;

        Vector2 toPlayer = player.Position - enemy.Position;
        float distance = toPlayer.Length();

        if (distance > 0.001f)
        {
            enemy.Velocity = toPlayer / distance * enemy.Stats.Get(StatId.MoveSpeed);
        }

        // Measured surface to surface, so a bigger chaser triggers where it
        // looks like it should rather than when its centre arrives.
        float gap = distance - enemy.Radius - player.Radius;

        if (gap <= enemy.Stats.Get(StatId.ProximityRadius))
        {
            Detonate(enemy, world);
        }
    }

    /// <summary>
    /// Note the two distances are measured differently, which is why the data
    /// must keep ProximityRadius well inside ExplosionRadius: the trigger is
    /// surface to surface, while blast falloff runs from the enemy centre to
    /// the player's surface. Tuned so a detonation at trigger range lands most
    /// of its damage rather than clipping the rim.
    /// </summary>
    private static void Detonate(Enemy enemy, World world)
    {
        float radius = enemy.Stats.Get(StatId.ExplosionRadius);
        float damage = enemy.Stats.Get(StatId.ContactDamage);

        Player player = world.Player;

        float distance = Vector2.Distance(enemy.Position, player.Position) - player.Radius;

        if (distance <= radius)
        {
            // Linear falloff, so backing off at the last moment reduces the hit.
            float falloff = 1f - MathF.Max(0f, distance) / radius;
            player.TakeDamage(damage * falloff);
        }

        world.AddExplosion(enemy.Position, radius);

        // Not a kill: it consumed itself, so it should award nothing.
        enemy.PendingRemoval = true;
    }
}

/// <summary>
/// Purple diamond. Holds a standoff band around the player and fires on a slow
/// cadence. Advances when too far, retreats when too close, and holds still in
/// between - the deadband is what stops it oscillating on the threshold.
/// </summary>
public sealed class ShooterBehavior : IEnemyBehavior
{
    /// <summary>Half-width of the band it will sit still inside, in world units.</summary>
    private const float BandHalfWidth = 40f;

    public void Tick(Enemy enemy, World world)
    {
        Player player = world.Player;

        Vector2 toPlayer = player.Position - enemy.Position;
        float distance = toPlayer.Length();
        if (distance < 0.001f) return;

        Vector2 direction = toPlayer / distance;

        float standoff = enemy.Stats.Get(StatId.StandoffDistance);
        float speed = enemy.Stats.Get(StatId.MoveSpeed);

        if (distance > standoff + BandHalfWidth) enemy.Velocity = direction * speed;
        else if (distance < standoff - BandHalfWidth) enemy.Velocity = -direction * speed;
        else enemy.Velocity = Vector2.Zero;

        TickFiring(enemy, world, direction);
    }

    private static void TickFiring(Enemy enemy, World world, Vector2 direction)
    {
        if (enemy.ActionCooldown > 0)
        {
            enemy.ActionCooldown--;
            return;
        }

        float rate = enemy.Stats.Get(StatId.FireRate);
        if (rate <= 0f) return;

        world.SpawnEnemyBullet(
            enemy.Position + direction * enemy.Radius,
            direction * enemy.Stats.Get(StatId.BulletSpeed),
            enemy.Stats.Get(StatId.Damage),
            enemy.Stats.Get(StatId.BulletLifetime));

        // Whole ticks, so cadence is exact under the fixed timestep.
        enemy.ActionCooldown = Math.Max(1, (int)MathF.Round(World.TickRate / rate));
    }
}

/// <summary>Placeholder until #22.</summary>
public sealed class SpawnerBehavior : IEnemyBehavior
{
    public void Tick(Enemy enemy, World world)
    {
    }
}
