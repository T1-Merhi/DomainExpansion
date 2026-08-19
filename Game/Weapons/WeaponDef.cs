// Minimal stubs so JSON loading can be proven end to end.
// #13 expands these with stats, projectile count, spread and on-hit behaviour.

public sealed class WeaponDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class WeaponCatalog
{
    public int SchemaVersion { get; set; }
    public List<WeaponDef> Weapons { get; set; } = new();
}
