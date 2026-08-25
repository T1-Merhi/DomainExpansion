/// <summary>
/// Purchase rules. Owns every path that spends coins, so affordability, level
/// caps and applying the effect can never drift apart between call sites.
///
/// Pricing and effects come entirely from UpgradeDef, so a new entry in
/// upgrades.json is purchasable without touching this class - except for the
/// kinds that need bespoke effects (Shape, Repair, EquipWeapon).
/// </summary>
public sealed class Shop
{
    private readonly World _world;

    public Shop(World world)
    {
        _world = world;
    }

    /// <summary>Current level of an upgrade for the given mount, or the player.</summary>
    public int LevelOf(UpgradeDef def, int mountIndex)
    {
        if (def == null || !IsValidMount(mountIndex)) return 0;

        return def.Kind == UpgradeKind.MountStat
            ? _world.Player.Mounts[mountIndex].Levels.Get(def.Id)
            : _world.Player.Levels.Get(def.Id);
    }

    public int CostOf(UpgradeDef def, int mountIndex) =>
        def == null ? 0 : def.CostFor(LevelOf(def, mountIndex));

    public bool IsMaxed(UpgradeDef def, int mountIndex) =>
        def != null && def.IsMaxed(LevelOf(def, mountIndex));

    public bool CanAfford(UpgradeDef def, int mountIndex) =>
        def != null && _world.Coins >= CostOf(def, mountIndex);

    /// <summary>
    /// True when the purchase is legal right now. Kept separate from Buy so the
    /// UI can grey an option out using exactly the rule that Buy enforces.
    /// </summary>
    public bool CanBuy(UpgradeDef def, int mountIndex)
    {
        if (def == null || !IsValidMount(mountIndex)) return false;
        if (IsMaxed(def, mountIndex)) return false;
        if (!CanAfford(def, mountIndex)) return false;

        return def.Kind switch
        {
            UpgradeKind.MountStat => !_world.Player.Mounts[mountIndex].IsEmpty,
            UpgradeKind.Shape => _world.Player.SideCount < Player.MaxSides,
            UpgradeKind.Repair => _world.Player.Health < _world.Player.MaxHealth,
            UpgradeKind.EquipWeapon => true,
            UpgradeKind.PlayerStat => true,
            UpgradeKind.ShieldHealth => true,
            UpgradeKind.ShieldRadius => true,
            _ => false,
        };
    }

    /// <summary>Attempts a purchase. Returns false and changes nothing if disallowed.</summary>
    public bool Buy(UpgradeDef def, int mountIndex)
    {
        if (!CanBuy(def, mountIndex)) return false;

        int cost = CostOf(def, mountIndex);
        if (!_world.TrySpendCoins(cost)) return false;

        Apply(def, mountIndex);
        return true;
    }

    /// <summary>
    /// Applies an upgrade without charging for it. Admin only - the test arena
    /// uses it to reach a late-game loadout instantly. Deliberately bypasses
    /// CanBuy's affordability check but still respects the level cap.
    /// </summary>
    public bool GrantFree(UpgradeDef def, int mountIndex)
    {
        if (def == null || IsMaxed(def, mountIndex)) return false;
        if (def.Kind == UpgradeKind.MountStat && _world.Player.Mounts[mountIndex].IsEmpty) return false;

        Apply(def, mountIndex);
        return true;
    }

    private void Apply(UpgradeDef def, int mountIndex)
    {
        Player player = _world.Player;

        switch (def.Kind)
        {
            case UpgradeKind.MountStat:
                player.Mounts[mountIndex].Weapon.Stats.AddModifier(def.ToModifier());
                player.Mounts[mountIndex].Levels.Increment(def.Id);
                break;

            case UpgradeKind.PlayerStat:
            {
                // Measure the change rather than assuming the modifier value,
                // so this stays correct for Mult as well as Add.
                float maxBefore = player.MaxHealth;

                player.Stats.AddModifier(def.ToModifier());
                player.Levels.Increment(def.Id);

                // Raising max health grants that health too, so the bar fills
                // rather than the fraction dropping the moment you pay for it.
                float gained = player.MaxHealth - maxBefore;
                if (gained > 0f) player.Heal(gained);
                break;
            }

            case UpgradeKind.Shape:
                player.AddSide();
                player.Levels.Increment(def.Id);
                break;

            case UpgradeKind.ShieldHealth:
                _world.Shield.AddMaxHealth(def.ValuePerLevel);
                player.Levels.Increment(def.Id);
                break;

            case UpgradeKind.ShieldRadius:
                _world.Shield.AddRadius(def.ValuePerLevel);
                player.Levels.Increment(def.Id);
                break;

            case UpgradeKind.Repair:
                // Never levels: repeatable at a flat cost, restores to full.
                player.HealFull();
                break;
        }
    }

    /// <summary>
    /// Equipping is separate from Buy because it needs the chosen weapon, which
    /// no generic upgrade signature carries.
    /// </summary>
    public bool BuyEquip(UpgradeDef def, int mountIndex, WeaponDef weapon)
    {
        if (def == null || weapon == null) return false;
        if (!IsValidMount(mountIndex)) return false;
        if (!CanBuy(def, mountIndex)) return false;

        int cost = CostOf(def, mountIndex);
        if (!_world.TrySpendCoins(cost)) return false;

        _world.Player.Mounts[mountIndex].Equip(weapon);
        return true;
    }

    /// <summary>
    /// Mounts exist up to MaxSidesCeiling but only SideCount are on the
    /// polygon, so an out-of-range index would equip a weapon onto a side that
    /// does not exist - or throw.
    /// </summary>
    private bool IsValidMount(int mountIndex) =>
        mountIndex >= 0 && mountIndex < _world.Player.SideCount;
}
