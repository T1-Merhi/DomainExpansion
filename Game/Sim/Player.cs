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

    /// <summary>
    /// One per potential side. Allocated to MaxSides up front so a shape
    /// upgrade only raises SideCount - mounts beyond it keep their contents,
    /// which matters if we ever allow shrinking.
    /// </summary>
    public readonly Mount[] Mounts = new Mount[MaxSides];

    /// <summary>
    /// Current HP. Kept as a plain field rather than a stat, because it is
    /// state that changes constantly; MaxHealth is the upgradeable stat.
    /// </summary>
    public float Health { get; private set; }

    public bool IsDead => Health <= 0f;

    public Player()
    {
        for (int i = 0; i < MaxSides; i++) Mounts[i] = new Mount();

        Stats.SetBase(StatId.MoveSpeed, 260f);
        Stats.SetBase(StatId.MaxHealth, 100f);

        Health = MaxHealth;
    }

    public float MaxHealth => Stats.Get(StatId.MaxHealth);

    public float HealthFraction
    {
        get
        {
            float max = MaxHealth;
            return max <= 0f ? 0f : Math.Clamp(Health / max, 0f, 1f);
        }
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead) return;

        Health = MathF.Max(0f, Health - amount);
    }

    /// <summary>Never exceeds MaxHealth, so #31's Repair cannot overheal.</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead) return;

        Health = MathF.Min(MaxHealth, Health + amount);
    }

    public Mount ActiveMount => Mounts[ActiveSide];

    /// <summary>
    /// Applies the movement axis for one tick. The axis is normalised by the
    /// caller, so diagonals are not faster than cardinals, and speed is per
    /// second rather than per tick so stat values read naturally.
    /// </summary>
    public void Move(Vector2 axis, Vector2 arenaSize)
    {
        if (axis.LengthSquared() > 0f)
        {
            Position += axis * Stats.Get(StatId.MoveSpeed) * World.FixedStep;
        }

        // Keep the whole polygon inside the arena, not just its centre.
        Position = new Vector2(
            Math.Clamp(Position.X, Radius, arenaSize.X - Radius),
            Math.Clamp(Position.Y, Radius, arenaSize.Y - Radius));
    }

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

    /// <summary>
    /// Rotation the polygon wants to reach so the active side faces the cursor.
    /// Rotation eases toward this rather than snapping, so switching weapons
    /// reads as the polygon spinning that side into place.
    /// </summary>
    public float TargetRotation { get; private set; }

    /// <summary>Fraction of the remaining angle closed per tick.</summary>
    public float TurnResponse = 0.35f;

    /// <summary>Sets the target rotation so the active side's normal points at the cursor.</summary>
    public void AimAt(Vector2 target)
    {
        Vector2 delta = target - Position;
        if (delta.LengthSquared() < 0.0001f) return;

        float angle = MathF.Atan2(delta.Y, delta.X);
        TargetRotation = angle - (2 * ActiveSide + 1) * MathF.PI / SideCount;
    }

    /// <summary>
    /// Eases Rotation toward TargetRotation along the shorter arc, so a switch
    /// that crosses the -pi/pi boundary does not unwind the long way round.
    /// </summary>
    public void StepRotation()
    {
        float diff = WrapAngle(TargetRotation - Rotation);
        Rotation = WrapAngle(Rotation + diff * TurnResponse);
    }

    /// <summary>Snaps instantly - used when the shape changes and easing would look wrong.</summary>
    public void SnapRotation() => Rotation = TargetRotation;

    /// <summary>Selects a side by offset, wrapping around the polygon.</summary>
    public void CycleActiveSide(int delta)
    {
        if (delta == 0 || SideCount <= 0) return;

        ActiveSide = ((ActiveSide + delta) % SideCount + SideCount) % SideCount;
    }

    /// <summary>Normalises to (-pi, pi].</summary>
    public static float WrapAngle(float radians)
    {
        radians = MathF.IEEERemainder(radians, MathF.Tau);
        return radians;
    }
}
