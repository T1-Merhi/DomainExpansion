public enum FloatingTextKind
{
    Damage,
    Coin,
}

/// <summary>
/// Short-lived readout that drifts upward from a world position. Pooled, since
/// sustained fire into a crowd produces these faster than anything else.
///
/// Damage and coin pickups share one pool: they have identical motion and
/// lifetime, and only differ in how the renderer styles them.
/// </summary>
public sealed class FloatingText : IPoolable
{
    public const int LifeTicks = 42;

    public Vector2 Position;
    public Vector2 Velocity;
    public float Amount;
    public int TicksLeft;
    public FloatingTextKind Kind;

    public void Reset()
    {
        Position = Vector2.Zero;
        Velocity = Vector2.Zero;
        Amount = 0f;
        TicksLeft = 0;
        Kind = FloatingTextKind.Damage;
    }

    /// <summary>0 at spawn, 1 when finished.</summary>
    public float Progress => 1f - TicksLeft / (float)LifeTicks;

    public void Step()
    {
        Position += Velocity * World.FixedStep;

        // Slow as it rises, so the text settles where it can be read.
        Velocity *= 0.94f;
        TicksLeft--;
    }
}
