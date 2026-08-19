/// <summary>
/// Base stats for one enemy type, loaded from enemies.json. Wave scaling in
/// #33 applies modifiers on top of a clone of these rather than editing them.
/// </summary>
public sealed class EnemyDef
{
    public string Type { get; set; } = "";
    public float Radius { get; set; } = 14f;
    public Dictionary<string, float> Stats { get; set; } = new();

    public EnemyType ParsedType =>
        Enum.TryParse<EnemyType>(Type, ignoreCase: true, out var t) ? t : EnemyType.Chaser;

    public StatBlock CreateStatBlock()
    {
        var block = new StatBlock();

        foreach (var kv in Stats)
        {
            if (Enum.TryParse<StatId>(kv.Key, ignoreCase: true, out var id))
                block.SetBase(id, kv.Value);
            else
                Console.WriteLine($"Enemies: '{Type}' has unknown stat '{kv.Key}', ignored");
        }

        return block;
    }
}

public sealed class EnemyCatalog
{
    public int SchemaVersion { get; set; }
    public List<EnemyDef> Enemies { get; set; } = new();

    public static EnemyCatalog Load()
    {
        var catalog = JsonData.Load<EnemyCatalog>("enemies.json");
        Console.WriteLine($"Enemies: loaded {catalog.Enemies.Count} definition(s)");
        return catalog;
    }

    public EnemyDef Find(EnemyType type)
    {
        foreach (var e in Enemies)
        {
            if (e.ParsedType == type) return e;
        }

        Console.WriteLine($"Enemies: no definition for type '{type}'");
        return null;
    }
}
