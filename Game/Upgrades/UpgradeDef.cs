public enum UpgradeKind
{
    /// <summary>Adds a modifier to one mount's weapon. Levelled per mount.</summary>
    MountStat,

    /// <summary>Adds a modifier to the player. Levelled once.</summary>
    PlayerStat,

    /// <summary>Adds a side to the polygon. Level tracks the current shape.</summary>
    Shape,

    /// <summary>Restores health. Repeatable, never levels.</summary>
    Repair,

    /// <summary>Fits a weapon to a mount. Repeatable, never levels.</summary>
    EquipWeapon,
}

/// <summary>
/// One purchasable upgrade, loaded from upgrades.json.
///
/// Cost is a formula rather than a table so levels can continue indefinitely:
/// cost(level) = CostBase * CostGrowth^level, with optional per-level
/// overrides for hand-tuning the early curve where it matters most.
/// </summary>
public sealed class UpgradeDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public UpgradeKind Kind { get; set; } = UpgradeKind.MountStat;

    /// <summary>StatId name. Unused for Repair, Shape and EquipWeapon.</summary>
    public string Stat { get; set; } = "";

    public ModifierOp Op { get; set; } = ModifierOp.Add;

    /// <summary>Modifier value granted by each level.</summary>
    public float ValuePerLevel { get; set; }

    /// <summary>Zero means unbounded.</summary>
    public int MaxLevel { get; set; }

    public float CostBase { get; set; } = 50f;
    public float CostGrowth { get; set; } = 1.35f;

    /// <summary>Level-indexed cost overrides, keyed by level as a string.</summary>
    public Dictionary<string, float> CostOverrides { get; set; } = new();

    public bool HasStat => !string.IsNullOrWhiteSpace(Stat);

    public StatId ParsedStat =>
        Enum.TryParse<StatId>(Stat, ignoreCase: true, out var id) ? id : StatId.Damage;

    public bool IsMaxed(int currentLevel) => MaxLevel > 0 && currentLevel >= MaxLevel;

    /// <summary>Cost to go from <paramref name="currentLevel"/> to the next one.</summary>
    public int CostFor(int currentLevel)
    {
        if (CostOverrides.TryGetValue(currentLevel.ToString(), out float exact))
            return (int)MathF.Round(exact);

        float cost = CostBase * MathF.Pow(CostGrowth, currentLevel);
        return (int)MathF.Round(cost);
    }

    /// <summary>The modifier a single purchase grants. Meaningless without a stat.</summary>
    public Modifier ToModifier() => new(ParsedStat, Op, ValuePerLevel);
}

public sealed class UpgradeCatalog
{
    public int SchemaVersion { get; set; }
    public List<UpgradeDef> Upgrades { get; set; } = new();

    public static UpgradeCatalog Load()
    {
        var catalog = JsonData.Load<UpgradeCatalog>("upgrades.json");
        Console.WriteLine($"Upgrades: loaded {catalog.Upgrades.Count} definition(s)");
        return catalog;
    }

    public UpgradeDef Find(string id)
    {
        foreach (var u in Upgrades)
        {
            if (string.Equals(u.Id, id, StringComparison.OrdinalIgnoreCase)) return u;
        }

        Console.WriteLine($"Upgrades: no definition with id '{id}'");
        return null;
    }
}
