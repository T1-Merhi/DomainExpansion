/// <summary>
/// JSON loading for config files.
///
/// Reads resolve against the shared config folder (see ConfigPaths), not the
/// build output, so every instance sees the same data.
/// </summary>
public static class JsonData
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // Without this, enums must be authored as integers - data files would
        // say 1 instead of "Explode", and any string value fails the whole file.
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) },
    };

    public static string PathFor(string fileName) => ConfigPaths.PathFor(fileName);

    /// <summary>
    /// Returns a default-constructed T rather than throwing when the file is
    /// missing or malformed, so a bad data edit cannot take the game down.
    /// </summary>
    public static T Load<T>(string fileName) where T : new()
    {
        string path = PathFor(fileName);
        string json = ConfigPaths.ReadWithRetry(path);

        if (json == null)
        {
            Console.WriteLine($"Data: '{fileName}' not readable at {path}");
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Data: failed to parse '{fileName}': {ex.Message}");
            return new T();
        }
    }
}
