/// <summary>How many of each type a wave contains.</summary>
public struct WaveComposition
{
    public int Chaser;
    public int Shooter;
    public int Spawner;

    public int Total => Chaser + Shooter + Spawner;

    public int CountOf(EnemyType type) => type switch
    {
        EnemyType.Chaser => Chaser,
        EnemyType.Shooter => Shooter,
        _ => Spawner,
    };
}

/// <summary>
/// Turns a wave index into a composition and a set of stat multipliers.
///
/// Everything is computed from the index rather than stored per wave, so waves
/// continue indefinitely; per-wave overrides exist only to hand-place the few
/// spikes that a smooth curve cannot express.
/// </summary>
public sealed class WaveGenerator
{
    private readonly WaveConfig _config;

    public WaveGenerator(WaveConfig config)
    {
        _config = config;
    }

    public WaveConfig Config => _config;

    public WaveComposition For(int wave)
    {
        wave = Math.Max(1, wave);

        return new WaveComposition
        {
            Chaser = CountFor(wave, "chaser"),
            Shooter = CountFor(wave, "shooter"),
            Spawner = CountFor(wave, "spawner"),
        };
    }

    private int CountFor(int wave, string type)
    {
        // An override replaces only the type it names, leaving the rest on curve.
        if (_config.Overrides.TryGetValue(wave.ToString(), out var overrides) &&
            overrides.TryGetValue(type, out int exact))
        {
            return Math.Max(0, exact);
        }

        return _config.Counts.TryGetValue(type, out CountCurve curve) ? curve.At(wave) : 0;
    }

    /// <summary>
    /// Applies this wave's scaling to a freshly spawned enemy as multiplicative
    /// modifiers, so the shared definition is never mutated and the enemy's own
    /// base values stay visible underneath.
    /// </summary>
    public void ApplyScaling(Enemy enemy, int wave)
    {
        if (enemy.Stats == null) return;

        foreach (var pair in _config.Scaling)
        {
            if (!Enum.TryParse<StatId>(pair.Key, ignoreCase: true, out StatId stat)) continue;

            float multiplier = pair.Value.At(wave);

            // StatBlock treats Mult as a fraction: 1.15x is expressed as +0.15.
            if (MathF.Abs(multiplier - 1f) > 0.0001f)
                enemy.Stats.AddModifier(Modifier.Mult(stat, multiplier - 1f));
        }

        // Health was resolved from MaxHealth before scaling existed, so refresh it.
        enemy.Health = enemy.Stats.Get(StatId.MaxHealth);
    }

    /// <summary>Current multiplier for a stat, for the debug and admin readouts.</summary>
    public float ScaleOf(string statName, int wave) =>
        _config.Scaling.TryGetValue(statName, out ScaleCurve curve) ? curve.At(wave) : 1f;
}
