/// <summary>
/// Three arcs orbiting the player, each with its own health.
///
/// The gaps between them are the mechanic, not a rendering detail: an intact
/// arc stops everything, so the player's job is to keep the openings pointed
/// away from whatever is shooting. A broken arc leaves a permanent hole until
/// the next wave repairs it.
/// </summary>
public sealed class Shield
{
    public const int ArcCount = 3;

    /// <summary>Health per arc. Index 0 starts at Rotation and they are evenly spaced.</summary>
    private readonly float[] _health = new float[ArcCount];

    /// <summary>Ticks of hit reaction per arc, for the renderer.</summary>
    private readonly int[] _flash = new int[ArcCount];

    /// <summary>Current orbit angle of arc 0, in radians.</summary>
    public float Rotation { get; private set; }

    public bool Enabled => Tuning.Player.ShieldEnabled && MaxArcHealth > 0f;

    /// <summary>Distance from the player centre to the middle of the arc band.</summary>
    public float Radius => Tuning.Player.ShieldRadius + _bonusRadius;

    public float Thickness => Tuning.Player.ShieldThickness;

    /// <summary>Per-arc maximum, raised by the Max Shield upgrade.</summary>
    public float MaxArcHealth => Tuning.Player.ShieldArcHealth + _bonusHealth;

    private float _bonusHealth;
    private float _bonusRadius;

    public Shield()
    {
        RepairAll();
    }

    public float HealthOf(int arc) => _health[arc];

    public bool IsIntact(int arc) => _health[arc] > 0f;

    public int FlashOf(int arc) => _flash[arc];

    public bool AnyIntact
    {
        get
        {
            for (int i = 0; i < ArcCount; i++) if (IsIntact(i)) return true;
            return false;
        }
    }

    public void AddMaxHealth(float amount)
    {
        if (amount <= 0f) return;

        _bonusHealth += amount;

        // Grant the new capacity rather than leaving arcs proportionally
        // weaker than before the purchase.
        for (int i = 0; i < ArcCount; i++)
            if (_health[i] > 0f) _health[i] = MathF.Min(MaxArcHealth, _health[i] + amount);
    }

    public void AddRadius(float amount)
    {
        if (amount > 0f) _bonusRadius += amount;
    }

    public void RepairAll()
    {
        for (int i = 0; i < ArcCount; i++) _health[i] = MaxArcHealth;
    }

    public void Tick()
    {
        float perTick = Tuning.Player.ShieldRotationSpeed * MathF.PI / 180f * World.FixedStep;
        Rotation = Player.WrapAngle(Rotation + perTick);

        for (int i = 0; i < ArcCount; i++)
            if (_flash[i] > 0) _flash[i]--;
    }

    /// <summary>Centre angle of an arc, in radians.</summary>
    public float CentreAngle(int arc) => Player.WrapAngle(Rotation + arc * MathF.Tau / ArcCount);

    public float HalfWidthRadians => Tuning.Player.ShieldArcDegrees * 0.5f * MathF.PI / 180f;

    /// <summary>
    /// The arc covering a world direction from the player, or -1 for a gap or a
    /// broken arc. Callers treat -1 as "nothing stops this".
    /// </summary>
    public int ArcCovering(Vector2 playerPosition, Vector2 point)
    {
        if (!Enabled) return -1;

        Vector2 delta = point - playerPosition;
        if (delta.LengthSquared() < 0.0001f) return -1;

        float angle = MathF.Atan2(delta.Y, delta.X);
        float half = HalfWidthRadians;

        for (int i = 0; i < ArcCount; i++)
        {
            if (!IsIntact(i)) continue;

            if (MathF.Abs(Player.WrapAngle(angle - CentreAngle(i))) <= half) return i;
        }

        return -1;
    }

    /// <summary>True when the point is inside the band an arc occupies.</summary>
    public bool WithinBand(Vector2 playerPosition, Vector2 point, float pointRadius)
    {
        float distance = Vector2.Distance(playerPosition, point);
        float half = Thickness * 0.5f + pointRadius;

        return distance >= Radius - half && distance <= Radius + half;
    }

    /// <summary>Applies damage to one arc. Returns true if it broke on this hit.</summary>
    public bool DamageArc(int arc, float amount)
    {
        if (arc < 0 || arc >= ArcCount || amount <= 0f || _health[arc] <= 0f) return false;

        _health[arc] = MathF.Max(0f, _health[arc] - amount);
        _flash[arc] = 6;

        return _health[arc] <= 0f;
    }
}
