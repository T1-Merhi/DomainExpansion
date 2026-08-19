public enum EnemyType
{
    /// <summary>Red square. Closes on the player and detonates.</summary>
    Chaser,

    /// <summary>Purple diamond. Keeps distance and shoots.</summary>
    Shooter,

    /// <summary>Orange pentagon. Emits other enemies, does not attack.</summary>
    Spawner,
}

public sealed class Enemy : IPoolable
{
    public EnemyType Type;
    public Vector2 Position;
    public Vector2 Velocity;
    public float Health;
    public float Radius;

    /// <summary>Per-instance stats, cloned from the type definition on spawn.</summary>
    public StatBlock Stats;

    /// <summary>Generic countdown used by behaviours for firing or spawning.</summary>
    public int ActionCooldown;

    /// <summary>Ticks of hit-reaction shake left. Purely cosmetic.</summary>
    public int HitShakeTicks;

    public static int HitShakeDuration => Tuning.Effects.HitShakeTicks;

    /// <summary>Set when the enemy should be removed after behaviours have run.</summary>
    public bool PendingRemoval;

    public bool IsDead => Health <= 0f;

    public void Reset()
    {
        Type = EnemyType.Chaser;
        Position = Vector2.Zero;
        Velocity = Vector2.Zero;
        Health = 0f;
        Radius = 0f;
        Stats = null;
        ActionCooldown = 0;
        HitShakeTicks = 0;
        PendingRemoval = false;
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        Health = MathF.Max(0f, Health - amount);

        // Restart the shake on every hit, so sustained fire keeps it shuddering
        // rather than the reaction lapsing between shots.
        HitShakeTicks = HitShakeDuration;
    }
}
