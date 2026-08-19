public sealed class LeaderboardEntry
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public int Wave { get; set; }
    public string Date { get; set; } = "";
}

/// <summary>
/// Top-five table persisted beside the executable.
///
/// Every failure path degrades to an empty board rather than throwing: losing
/// a high-score table is an annoyance, crashing on startup because of a
/// malformed file is not acceptable.
/// </summary>
public sealed class Leaderboard
{
    public const int Capacity = 5;
    private const string FileName = "leaderboard.json";

    public List<LeaderboardEntry> Entries { get; set; } = new();

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, FileName);

    public static Leaderboard Load()
    {
        try
        {
            if (!File.Exists(Path)) return new Leaderboard();

            var loaded = JsonSerializer.Deserialize<Leaderboard>(File.ReadAllText(Path));
            if (loaded == null) return new Leaderboard();

            loaded.Normalise();
            return loaded;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Leaderboard: could not load ({ex.Message}); starting empty");
            return new Leaderboard();
        }
    }

    public void Save()
    {
        try
        {
            // Temp then replace, so a reader never sees a half-written table.
            string temp = Path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, WriteOptions));
            File.Move(temp, Path, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Leaderboard: could not save ({ex.Message})");
        }
    }

    /// <summary>True when this score would make the table.</summary>
    public bool Qualifies(int score)
    {
        if (score <= 0) return false;
        if (Entries.Count < Capacity) return true;

        return score > Entries[Entries.Count - 1].Score;
    }

    /// <summary>Inserts and trims, returning the new entry's index or -1.</summary>
    public int Insert(string name, int score, int wave)
    {
        if (!Qualifies(score)) return -1;

        var entry = new LeaderboardEntry
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Anonymous" : name.Trim(),
            Score = score,
            Wave = wave,
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
        };

        Entries.Add(entry);
        Normalise();

        return Entries.IndexOf(entry);
    }

    /// <summary>Sorts descending and trims to capacity, tolerating a hand-edited file.</summary>
    private void Normalise()
    {
        Entries.RemoveAll(e => e == null);
        Entries.Sort((a, b) => b.Score.CompareTo(a.Score));

        if (Entries.Count > Capacity) Entries.RemoveRange(Capacity, Entries.Count - Capacity);
    }
}
