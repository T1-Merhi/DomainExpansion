/// <summary>
/// Result of the most recent run, handed from GameScene to DeathScene.
///
/// Static because IScene.Init takes only AssetManager and GameSettings, and
/// SceneManager constructs scenes with no arguments - there is no injection
/// point for per-run data. Deliberately kept to plain values with no
/// behaviour, so the coupling stays visible and trivial to replace if the
/// scene contract ever gains a payload.
/// </summary>
public static class RunResult
{
    public static int Score;
    public static int Coins;
    public static int Wave;

    public static void Capture(World world)
    {
        Score = world.Score;
        Coins = world.Coins;
        Wave = 0; // #35 supplies the wave the run ended on.
    }

    public static void Clear()
    {
        Score = 0;
        Coins = 0;
        Wave = 0;
    }
}
