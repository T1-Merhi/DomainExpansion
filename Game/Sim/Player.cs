/// <summary>
/// The player polygon. Sides are weapon mounts, numbered anticlockwise from
/// vertex 0. Side <see cref="ActiveSide"/> is the one that fires, and the whole
/// polygon rotates so that side faces the cursor - so aiming and weapon
/// selection are the same act.
///
/// Geometry: vertex i sits at angle Rotation + i*(2pi/N) on the circumcircle.
/// Side i spans vertex i to vertex i+1, so its outward normal is halfway
/// between them, at Rotation + (2i+1)*(pi/N), and its midpoint sits at the
/// apothem along that normal.
/// </summary>
public sealed class Player
{
    public const int MaxSides = 12;

    public Vector2 Position;
    public float Rotation;          // radians, angle of vertex 0
    public float Radius = 34f;      // circumradius
    public int SideCount = 3;       // triangle
    public int ActiveSide;          // #15 drives this with the mouse wheel

    public readonly StatBlock Stats = new();

    /// <summary>Centre-to-side-midpoint distance.</summary>
    public float Apothem => Radius * MathF.Cos(MathF.PI / SideCount);

    public void GetVertices(Span<Vector2> dest)
    {
        float step = MathF.Tau / SideCount;
        for (int i = 0; i < SideCount; i++)
        {
            float a = Rotation + i * step;
            dest[i] = Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * Radius;
        }
    }

    /// <summary>Direction the given side's barrel points, in radians.</summary>
    public float SideNormalAngle(int side) =>
        Rotation + (2 * side + 1) * MathF.PI / SideCount;

    public Vector2 SideNormal(int side)
    {
        float a = SideNormalAngle(side);
        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    /// <summary>Barrel position for the given side.</summary>
    public Vector2 SideMidpoint(int side) => Position + SideNormal(side) * Apothem;

    /// <summary>Rotates so the active side's normal points at <paramref name="target"/>.</summary>
    public void AimAt(Vector2 target)
    {
        Vector2 delta = target - Position;
        if (delta.LengthSquared() < 0.0001f) return;

        float angle = MathF.Atan2(delta.Y, delta.X);
        Rotation = angle - (2 * ActiveSide + 1) * MathF.PI / SideCount;
    }
}
