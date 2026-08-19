/// <summary>
/// Base stats for one enemy type, loaded from enemies.json. Wave scaling in
/// #33 applies modifiers on top of a clone of these rather than editing them.
/// </summary>
public sealed class EnemyDef
{
    public string Type { get; set; } = "";
    public float Radius { get; set; } = 14f;

    /// <summary>Body colour as "RRGGBB". Gameplay-meaningful: it is how type is identified.</summary>
    public string Tint { get; set; } = "";

    public Dictionary<string, float> Stats { get; set; } = new();

    private uint _packedTint;
    private bool _tintParsed;

    public uint PackedTint
    {
        get
        {
            if (!_tintParsed)
            {
                _packedTint = ColorHex.Parse(Tint);
                _tintParsed = true;
            }

            return _packedTint;
        }
    }

    /// <summary>
    /// Falls back to Chaser on an unrecognised name, but says so - silently
    /// turning a typo into a chaser hides the real problem.
    /// </summary>
    public EnemyType ParsedType
    {
        get
        {
            if (Enum.TryParse<EnemyType>(Type, ignoreCase: true, out var t)) return t;

            if (!_typeWarned)
            {
                _typeWarned = true;
                Console.WriteLine($"Enemies: unknown type '{Type}', falling back to Chaser");
            }

            return EnemyType.Chaser;
        }
    }

    private bool _typeWarned;

    /// <summary>
    /// True when this enemy delivers its damage by detonating rather than by
    /// touching, so contact damage must not also apply.
    /// </summary>
    public bool Detonates { get; set; }

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
