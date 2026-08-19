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

/// <summary>Placeholder until #19. Drifts toward the player so spawns are visible.</summary>
public sealed class ChaserBehavior : IEnemyBehavior
{
    public void Tick(Enemy enemy, World world)
    {
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
