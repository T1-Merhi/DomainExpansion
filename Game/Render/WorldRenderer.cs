/// <summary>
/// Draws the simulation. Reads World state, never mutates it.
/// </summary>
public sealed class WorldRenderer
{
    public bool ShowDebug = true;

    public void Draw(World world, int stepsLastFrame)
    {
        if (ShowDebug) DrawDebugOverlay(world, stepsLastFrame);
    }

    private void DrawDebugOverlay(World world, int stepsLastFrame)
    {
        int y = Raylib.GetScreenHeight() - 70;
        Raylib.DrawText($"tick {world.TickCount}", 20, y, 18, Color.Gray);
        Raylib.DrawText($"steps/frame {stepsLastFrame}", 20, y + 22, 18, Color.Gray);
        Raylib.DrawText($"sim {(world.TickCount / (double)World.TickRate):F1}s", 200, y, 18, Color.Gray);
        Raylib.DrawText($"fps {Raylib.GetFPS()}", 200, y + 22, 18, Color.Gray);
    }
}
