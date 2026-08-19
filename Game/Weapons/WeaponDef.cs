public enum OnHit
{
    /// <summary>Damage the single target hit.</summary>
    Direct,

    /// <summary>Damage everything inside ExplosionRadius of the impact.</summary>
    Explode,
}

/// <summary>
/// Immutable weapon template loaded from weapons.json. Shared by every mount
/// carrying this weapon - per-mount upgrades live on the WeaponInstance.
/// </summary>
public sealed class WeaponDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public OnHit OnHit { get; set; } = OnHit.Direct;

    /// <summary>Base stat values, keyed by StatId name in JSON.</summary>
    public Dictionary<string, float> Stats { get; set; } = new();

    public StatBlock CreateStatBlock()
    {
        var block = new StatBlock();

        foreach (var kv in Stats)
        {
            if (Enum.TryParse<StatId>(kv.Key, ignoreCase: true, out var id))
                block.SetBase(id, kv.Value);
            else
                Console.WriteLine($"Weapons: '{Id}' has unknown stat '{kv.Key}', ignored");
        }

        return block;
    }
}

public sealed class WeaponCatalog
{
    public int SchemaVersion { get; set; }
    public List<WeaponDef> Weapons { get; set; } = new();

    public static WeaponCatalog Load()
    {
        var catalog = JsonData.Load<WeaponCatalog>("weapons.json");
        Console.WriteLine($"Weapons: loaded {catalog.Weapons.Count} definition(s)");
        return catalog;
    }

    public WeaponDef Find(string id)
    {
        foreach (var w in Weapons)
        {
            if (string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase)) return w;
        }

        Console.WriteLine($"Weapons: no definition with id '{id}'");
        return null;
    }
}
