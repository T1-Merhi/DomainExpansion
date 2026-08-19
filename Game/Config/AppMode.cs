/// <summary>
/// Player or admin, chosen at startup from the command line.
///
/// A runtime flag rather than a compilation symbol, so one build serves both
/// modes and two instances of the same binary can run side by side - which is
/// the entire point of the tuning workflow.
/// </summary>
public static class AppMode
{
    public static bool IsAdmin { get; private set; }

    public static void Parse(string[] args)
    {
        if (args == null) return;

        foreach (string arg in args)
        {
            if (string.Equals(arg, "--admin", StringComparison.OrdinalIgnoreCase))
                IsAdmin = true;
        }

        Console.WriteLine($"Mode: {(IsAdmin ? "ADMIN" : "PLAYER")}");
    }
}
