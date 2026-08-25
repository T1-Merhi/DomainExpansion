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
    public static int MaxSides => Tuning.Player.MaxSides;
    public const int MaxSidesCeiling = 24;

    public Vector2 Position;
    public float Rotation;          // radians, angle of vertex 0
    public float Radius = Tuning.Player.Radius;   // circumradius
    public int SideCount = 3;       // triangle
    public int ActiveSide;          // #15 drives this with the mouse wheel

    public readonly StatBlock Stats = new();

    /// <summary>
    /// One per potential side. Allocated to MaxSides up front so a shape
    /// upgrade only raises SideCount - mounts beyond it keep their contents,
    /// which matters if we ever allow shrinking.
    /// </summary>
    public readonly Mount[] Mounts = new Mount[MaxSidesCeiling];

    /// <summary>Player-wide upgrade levels: max health, move speed, shape.</summary>
    public readonly UpgradeLevels Levels = new();

    /// <summary>
    /// Current HP. Kept as a plain field rather than a stat, because it is
    /// state that changes constantly; MaxHealth is the upgradeable stat.
    /// </summary>
    public float Health { get; private set; }

    public bool IsDead => Health <= 0f;

    /// <summary>
    /// Ticks of hit reaction left. Deliberately longer than the enemy shake so
    /// taking damage reads as a different event from dealing it.
    /// </summary>
    public int HitFlashTicks;

    public static int HitFlashDuration => Tuning.Player.HitFlashTicks;

    public Player()
    {
        for (int i = 0; i < MaxSidesCeiling; i++) Mounts[i] = new Mount();

        Stats.SetBase(StatId.MoveSpeed, Tuning.Player.MoveSpeed);
        Stats.SetBase(StatId.MaxHealth, Tuning.Player.MaxHealth);

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
        HitFlashTicks = HitFlashDuration;
    }

    /// <summary>Ticks of muzzle flash left on the active barrel.</summary>
    public int MuzzleFlashTicks;

    public static int MuzzleFlashDuration => Tuning.Player.MuzzleFlashTicks;

    public void TickMuzzleFlash()
    {
        if (MuzzleFlashTicks > 0) MuzzleFlashTicks--;
    }

    // --- Dash -----------------------------------------------------------

    private Vector2 _dashDirection;
    private int _dashTicksLeft;
    private int _dashCooldownTicks;

    /// <summary>Invulnerable for the duration, which is the point of the dash.</summary>
    public bool IsDashing => _dashTicksLeft > 0;

    public bool DashReady => _dashCooldownTicks <= 0 && !IsDashing;

    /// <summary>0 when ready, 1 immediately after dashing.</summary>
    public float DashCooldownFraction
    {
        get
        {
            int total = DashCooldownTicks;
            return total <= 0 ? 0f : Math.Clamp(_dashCooldownTicks / (float)total, 0f, 1f);
        }
    }

    private static int DashCooldownTicks =>
        Math.Max(1, (int)MathF.Round(Tuning.Player.DashCooldownSeconds * World.TickRate));

    /// <summary>
    /// Dashes along the movement axis, or toward the cursor when standing
    /// still - a dash that does nothing because no key is held is just a
    /// wasted cooldown.
    /// </summary>
    public bool TryDash(Vector2 moveAxis, Vector2 cursor)
    {
        if (!DashReady) return false;

        Vector2 direction = moveAxis;

        if (direction.LengthSquared() < 0.001f)
        {
            Vector2 toCursor = cursor - Position;
            if (toCursor.LengthSquared() < 0.001f) return false;

            direction = toCursor;
        }

        _dashDirection = Vector2.Normalize(direction);
        _dashTicksLeft = Math.Max(1, (int)MathF.Round(Tuning.Player.DashSeconds * World.TickRate));
        _dashCooldownTicks = DashCooldownTicks;
        return true;
    }

    public void TickDash(Vector2 arenaSize)
    {
        if (_dashCooldownTicks > 0) _dashCooldownTicks--;
        if (_dashTicksLeft <= 0) return;

        _dashTicksLeft--;

        Position += _dashDirection * Tuning.Player.DashSpeed * World.FixedStep;

        Position = new Vector2(
            Math.Clamp(Position.X, Radius, arenaSize.X - Radius),
            Math.Clamp(Position.Y, Radius, arenaSize.Y - Radius));
    }

    public void TickHitFlash()
    {
        if (HitFlashTicks > 0) HitFlashTicks--;
    }

    /// <summary>1 at the moment of impact, decaying to 0.</summary>
    public float HitFlashStrength => HitFlashTicks <= 0
        ? 0f
        : HitFlashTicks / (float)HitFlashDuration;

    /// <summary>Never exceeds MaxHealth, so healing cannot overheal.</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead) return;

        Health = MathF.Min(MaxHealth, Health + amount);
    }

    public void HealFull()
    {
        if (IsDead) return;

        Health = MaxHealth;
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
    public float TurnResponse = Tuning.Player.TurnResponse;

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

    /// <summary>
    /// Grows the polygon by one side. The new mount is empty rather than
    /// locked - per the design, sides are never locked, only unfilled - and
    /// existing mounts keep their weapons and levels untouched.
    /// </summary>
    public bool AddSide()
    {
        if (SideCount >= MaxSides) return false;

        SideCount++;
        return true;
    }

    /// <summary>Selects a side by offset, wrapping around the polygon.</summary>
    public void CycleActiveSide(int delta)
    {
        if (delta == 0 || SideCount <= 0) return;

        ActiveSide = ((ActiveSide + delta) % SideCount + SideCount) % SideCount;
    }

    /// <summary>Normalises to [-pi, pi] - IEEERemainder is symmetric about zero.</summary>
    public static float WrapAngle(float radians)
    {
        radians = MathF.IEEERemainder(radians, MathF.Tau);
        return radians;
    }
}
