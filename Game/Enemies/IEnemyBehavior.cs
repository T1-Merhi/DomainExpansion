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

/// <summary>Placeholder until #20.</summary>
public sealed class ShooterBehavior : IEnemyBehavior
{
    public void Tick(Enemy enemy, World world)
    {
    }
}

/// <summary>Placeholder until #22.</summary>
public sealed class SpawnerBehavior : IEnemyBehavior
{
    public void Tick(Enemy enemy, World world)
    {
    }
}
