/// <summary>How a per-type enemy count grows with the wave index.</summary>
public sealed class CountCurve
{
    public float Base { get; set; }
    public float PerWave { get; set; }

    /// <summary>Wave 1 yields Base, so the authored numbers read as "wave one".</summary>
    public int At(int wave) => Math.Max(0, (int)MathF.Round(Base + PerWave * (wave - 1)));
}

/// <summary>
/// A multiplier that drifts with the wave index. Min and Max use zero to mean
/// "unbounded", which is safe here because every multiplier is positive.
/// </summary>
public sealed class ScaleCurve
{
    public float Base { get; set; } = 1f;
    public float PerWave { get; set; }
    public float Min { get; set; }
    public float Max { get; set; }

    public float At(int wave)
    {
        float value = Base + PerWave * (wave - 1);

        if (Min > 0f) value = MathF.Max(value, Min);
        if (Max > 0f) value = MathF.Min(value, Max);

        return MathF.Max(0f, value);
    }
}

public sealed class WaveConfig
{
    public int SchemaVersion { get; set; }

    public float RestSeconds { get; set; } = 20f;

    /// <summary>Enemies released per spawn beat, so a wave arrives in waves.</summary>
    public int SpawnBatchSize { get; set; } = 4;

    public float SpawnIntervalSeconds { get; set; } = 0.4f;

    public Dictionary<string, CountCurve> Counts { get; set; } = new();
    public Dictionary<string, ScaleCurve> Scaling { get; set; } = new();

    /// <summary>Per-wave count overrides, keyed by wave number then enemy type.</summary>
    public Dictionary<string, Dictionary<string, int>> Overrides { get; set; } = new();

    public static WaveConfig Load()
    {
        var config = JsonData.Load<WaveConfig>("waves.json");
        Console.WriteLine($"Waves: loaded {config.Counts.Count} count curve(s), {config.Scaling.Count} scaling curve(s)");
        return config;
    }
}
