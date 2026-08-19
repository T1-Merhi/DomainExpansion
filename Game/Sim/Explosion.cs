/// <summary>
/// Short-lived visual for a detonation. Purely cosmetic - the damage is applied
/// at the moment of detonation, not by this.
/// </summary>
public sealed class Explosion : IPoolable
{
    public const int LifeTicks = 12;

    public Vector2 Position;
    public float Radius;
    public int TicksLeft;

    /// <summary>Packed RGBA, opaque to the sim - lets a death burst read differently to a blast.</summary>
    public uint Tint;

    public void Reset()
    {
        Position = Vector2.Zero;
        Radius = 0f;
        TicksLeft = 0;
        Tint = 0xFFFFFFFF;
    }

    /// <summary>0 at spawn, 1 when finished.</summary>
    public float Progress => 1f - TicksLeft / (float)LifeTicks;
}
