/// <summary>
/// Floating damage readout. Pooled like everything else, because sustained
/// fire against a crowd produces these faster than anything else in the game.
/// </summary>
public sealed class DamageNumber : IPoolable
{
    public const int LifeTicks = 42;

    public Vector2 Position;
    public Vector2 Velocity;
    public float Amount;
    public int TicksLeft;

    public void Reset()
    {
        Position = Vector2.Zero;
        Velocity = Vector2.Zero;
        Amount = 0f;
        TicksLeft = 0;
    }

    /// <summary>0 at spawn, 1 when finished.</summary>
    public float Progress => 1f - TicksLeft / (float)LifeTicks;

    public void Step()
    {
        Position += Velocity * World.FixedStep;

        // Slow as it rises, so the number settles where it can be read.
        Velocity *= 0.94f;
        TicksLeft--;
    }
}
