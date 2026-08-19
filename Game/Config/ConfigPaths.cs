/// <summary>
/// Where configuration lives.
///
/// The canonical location is a single shared folder under AppData, NOT the
/// build output. Two instances must read the same files even when launched
/// from different build configurations, and per-output copies would let a
/// Debug and a Release instance silently diverge - which is precisely the
/// failure the two-instance workflow cannot tolerate.
///
/// The build output keeps a pristine, read-only copy that seeds the shared
/// folder on first run and backs reset-to-default forever after.
/// </summary>
public static class ConfigPaths
{
    public const string FolderName = "DomainExpansion";

    /// <summary>Shipped defaults, next to the executable. Never written to.</summary>
    public static string DefaultsDir => Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>The one location both instances read and write.</summary>
    public static string SharedDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        FolderName,
        "Data");

    public static string PathFor(string fileName) => Path.Combine(SharedDir, fileName);

    public static string DefaultPathFor(string fileName) => Path.Combine(DefaultsDir, fileName);

    /// <summary>
    /// Creates the shared folder and copies over any file it is missing.
    /// Copies per-file rather than all-or-nothing, so a config added in a later
    /// version appears for an existing install without wiping their edits.
    /// </summary>
    public static void EnsureSeeded()
    {
        try
        {
            Directory.CreateDirectory(SharedDir);

            if (!Directory.Exists(DefaultsDir))
            {
                Console.WriteLine($"Config: no shipped defaults at {DefaultsDir}");
                return;
            }

            foreach (string source in Directory.GetFiles(DefaultsDir, "*.json"))
            {
                string target = Path.Combine(SharedDir, Path.GetFileName(source));
                if (!File.Exists(target)) File.Copy(source, target);
            }

            Console.WriteLine($"Config: using {SharedDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Config: could not prepare shared folder ({ex.Message})");
        }
    }

    /// <summary>Restores one file from the shipped defaults, discarding edits.</summary>
    public static bool ResetToDefault(string fileName)
    {
        try
        {
            string source = DefaultPathFor(fileName);
            if (!File.Exists(source)) return false;

            AtomicWrite(PathFor(fileName), File.ReadAllText(source));
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Config: could not reset '{fileName}' ({ex.Message})");
            return false;
        }
    }

    /// <summary>
    /// Temp file then replace, so a concurrently reading instance never
    /// observes a partially written file.
    /// </summary>
    public static bool AtomicWrite(string path, string contents)
    {
        try
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents);
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Config: write failed for '{path}' ({ex.Message})");
            return false;
        }
    }

    /// <summary>
    /// Reads with retries, because the other instance may be mid-replace.
    /// Returns null on give-up so the caller can fall back to what it has.
    /// </summary>
    public static string ReadWithRetry(string path, int attempts = 4, int delayMs = 25)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(delayMs);
            }
        }

        return null;
    }
}
