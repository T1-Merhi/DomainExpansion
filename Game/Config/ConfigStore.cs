/// <summary>
/// Every config file, loaded together and versioned as a set.
///
/// Reload is the whole point: the player instance calls it when a run starts,
/// so edits saved by an admin instance take effect on restart without a
/// relaunch. A failed reload keeps the previous in-memory copy and records a
/// warning rather than leaving the game with nothing.
/// </summary>
public sealed class ConfigStore
{
    public static ConfigStore Current { get; } = new();

    public WeaponCatalog Weapons { get; private set; } = new();
    public EnemyCatalog Enemies { get; private set; } = new();
    public UpgradeCatalog Upgrades { get; private set; } = new();
    public WaveConfig Waves { get; private set; } = new();
    public PlayerConfig Player { get; private set; } = new();
    public EffectsConfig Effects { get; private set; } = new();

    /// <summary>Increments on every successful reload, so the HUD can prove it happened.</summary>
    public int Version { get; private set; }

    public DateTime LoadedAt { get; private set; }

    /// <summary>Non-empty when the last reload fell back to the previous copy.</summary>
    public string LastWarning { get; private set; } = "";

    public bool HasWarning => !string.IsNullOrEmpty(LastWarning);

    private bool _seeded;

    public void Reload()
    {
        if (!_seeded)
        {
            ConfigPaths.EnsureSeeded();
            _seeded = true;
        }

        var warnings = new List<string>();

        Weapons = LoadOr(Weapons, "weapons.json", warnings, c => c.Weapons.Count > 0);
        Enemies = LoadOr(Enemies, "enemies.json", warnings, c => c.Enemies.Count > 0);
        Upgrades = LoadOr(Upgrades, "upgrades.json", warnings, c => c.Upgrades.Count > 0);
        Waves = LoadOr(Waves, "waves.json", warnings, c => c.Counts.Count > 0);
        Player = LoadOr(Player, "player.json", warnings, c => c.MaxHealth > 0f);
        Effects = LoadOr(Effects, "effects.json", warnings, c => c.HitShakeTicks > 0);

        Tuning.Apply(Player, Effects);

        LastWarning = warnings.Count == 0 ? "" : string.Join("; ", warnings);
        LoadedAt = DateTime.Now;
        Version++;

        Console.WriteLine($"Config: v{Version} loaded at {LoadedAt:HH:mm:ss}" +
                          (HasWarning ? $" with warnings: {LastWarning}" : ""));
    }

    /// <summary>
    /// Loads one file, validating the result. Anything that fails validation
    /// keeps the previous value, so a typo in the editor degrades one file
    /// rather than taking down the run.
    /// </summary>
    private static T LoadOr<T>(T previous, string fileName, List<string> warnings, Func<T, bool> isValid)
        where T : class, new()
    {
        string json = ConfigPaths.ReadWithRetry(ConfigPaths.PathFor(fileName));

        if (json == null)
        {
            warnings.Add($"{fileName} unreadable");
            return previous;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<T>(json, JsonData.Options);

            if (loaded == null || !isValid(loaded))
            {
                warnings.Add($"{fileName} invalid");
                return previous;
            }

            return loaded;
        }
        catch (JsonException ex)
        {
            warnings.Add($"{fileName}: {ex.Message}");
            return previous;
        }
    }
}
