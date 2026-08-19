/// <summary>
/// Base values plus a list of modifiers, resolved as:
///   (base + sum of Add) * product of (1 + Mult)
///
/// Results are cached until a modifier or base value changes, since Get is
/// called from the tick loop for every entity.
/// </summary>
public sealed class StatBlock
{
    private readonly Dictionary<StatId, float> _base = new();
    private readonly List<Modifier> _modifiers = new();
    private readonly Dictionary<StatId, float> _cache = new();
    private bool _dirty = true;

    public StatBlock() { }

    public StatBlock(IEnumerable<KeyValuePair<StatId, float>> baseValues)
    {
        foreach (var kv in baseValues) _base[kv.Key] = kv.Value;
    }

    public IReadOnlyList<Modifier> Modifiers => _modifiers;

    public void SetBase(StatId stat, float value)
    {
        _base[stat] = value;
        _dirty = true;
    }

    public float GetBase(StatId stat) => _base.TryGetValue(stat, out var v) ? v : 0f;

    public void AddModifier(Modifier modifier)
    {
        _modifiers.Add(modifier);
        _dirty = true;
    }

    public bool RemoveModifier(Modifier modifier)
    {
        bool removed = _modifiers.Remove(modifier);
        if (removed) _dirty = true;
        return removed;
    }

    public void ClearModifiers()
    {
        if (_modifiers.Count == 0) return;
        _modifiers.Clear();
        _dirty = true;
    }

    public float Get(StatId stat)
    {
        if (_dirty) Rebuild();
        return _cache.TryGetValue(stat, out var v) ? v : 0f;
    }

    public int GetInt(StatId stat) => (int)MathF.Round(Get(stat));

    /// <summary>Recomputes every stat that has either a base value or a modifier.</summary>
    private void Rebuild()
    {
        _cache.Clear();

        foreach (var kv in _base) _cache[kv.Key] = kv.Value;

        // Adds first, so multipliers scale the full base+flat total.
        foreach (var m in _modifiers)
        {
            if (m.Op != ModifierOp.Add) continue;
            _cache[m.Stat] = (_cache.TryGetValue(m.Stat, out var v) ? v : 0f) + m.Value;
        }

        foreach (var m in _modifiers)
        {
            if (m.Op != ModifierOp.Mult) continue;
            _cache[m.Stat] = (_cache.TryGetValue(m.Stat, out var v) ? v : 0f) * (1f + m.Value);
        }

        _dirty = false;
    }

    /// <summary>Independent copy - used when spawning an entity from a shared definition.</summary>
    public StatBlock Clone()
    {
        var clone = new StatBlock();
        foreach (var kv in _base) clone._base[kv.Key] = kv.Value;
        clone._modifiers.AddRange(_modifiers);
        clone._dirty = true;
        return clone;
    }
}
