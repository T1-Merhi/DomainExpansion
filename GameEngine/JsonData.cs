/// <summary>
/// Loads JSON definition files from the Data folder next to the executable.
/// Paths resolve against AppContext.BaseDirectory, not the working directory,
/// so the game behaves the same whether launched via `dotnet run` or directly.
/// </summary>
public static class JsonData
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string PathFor(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Data", fileName);

    /// <summary>
    /// Returns a default-constructed T rather than throwing when the file is
    /// missing or malformed, so a bad data edit cannot take the game down.
    /// </summary>
    public static T Load<T>(string fileName) where T : new()
    {
        string path = PathFor(fileName);

        if (!File.Exists(path))
        {
            Console.WriteLine($"Data: '{fileName}' not found at {path}");
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Data: failed to parse '{fileName}': {ex.Message}");
            return new T();
        }
    }
}
