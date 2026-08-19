/// <summary>
/// One weapon fitted to one mount. Wraps the shared definition with a private
/// StatBlock, so upgrading damage on one side does not affect an identical
/// weapon on another side.
/// </summary>
public sealed class WeaponInstance
{
    public readonly WeaponDef Def;
    public readonly StatBlock Stats;

    /// <summary>Ticks remaining before this weapon may fire again.</summary>
    public int CooldownTicks;

    public WeaponInstance(WeaponDef def)
    {
        Def = def;
        Stats = def.CreateStatBlock();
    }

    public string Name => Def.Name;

    /// <summary>Shots per second converted to a whole number of ticks.</summary>
    public int CooldownForOneShot()
    {
        float rate = Stats.Get(StatId.FireRate);
        if (rate <= 0f) return int.MaxValue;

        return Math.Max(1, (int)MathF.Round(World.TickRate / rate));
    }

    public bool IsReady => CooldownTicks <= 0;

    public void TickCooldown()
    {
        if (CooldownTicks > 0) CooldownTicks--;
    }

    public void StartCooldown() => CooldownTicks = CooldownForOneShot();
}

/// <summary>
/// One side of the polygon. A mount with no weapon is an empty slot the player
/// can equip later - sides are never locked, only unfilled.
/// </summary>
public sealed class Mount
{
    public WeaponInstance Weapon;

    public int DamageLevel;
    public int FireRateLevel;

    public bool IsEmpty => Weapon == null;

    public void Equip(WeaponDef def)
    {
        Weapon = def == null ? null : new WeaponInstance(def);
        DamageLevel = 0;
        FireRateLevel = 0;
    }
}
