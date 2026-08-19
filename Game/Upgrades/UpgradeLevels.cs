/// <summary>
/// Per-owner upgrade levels, keyed by upgrade id.
///
/// Keyed by id rather than by named fields so a new upgrade added to
/// upgrades.json needs no code change to become levellable - which is the
/// whole point of the data-driven catalogue.
/// </summary>
public sealed class UpgradeLevels
{
    private readonly Dictionary<string, int> _levels = new();

    public int Get(string upgradeId) =>
        _levels.TryGetValue(upgradeId, out int level) ? level : 0;

    public void Set(string upgradeId, int level) => _levels[upgradeId] = level;

    public int Increment(string upgradeId)
    {
        int next = Get(upgradeId) + 1;
        _levels[upgradeId] = next;
        return next;
    }

    public void Clear() => _levels.Clear();
}
