public sealed class Bullet : IPoolable
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Damage;
    public float Radius;

    /// <summary>Ticks left before it despawns, so shotgun pellets have range.</summary>
    public int LifeTicks;

    /// <summary>Area damage on impact; zero means a direct hit only.</summary>
    public float ExplosionRadius;

    public void Reset()
    {
        Position = Vector2.Zero;
        Velocity = Vector2.Zero;
        Damage = 0f;
        Radius = 0f;
        LifeTicks = 0;
        ExplosionRadius = 0f;
    }

    public void Step()
    {
        Position += Velocity * World.FixedStep;
        LifeTicks--;
    }

    public bool Expired(Vector2 arenaSize) =>
        LifeTicks <= 0 ||
        Position.X < -Radius || Position.Y < -Radius ||
        Position.X > arenaSize.X + Radius || Position.Y > arenaSize.Y + Radius;
}
