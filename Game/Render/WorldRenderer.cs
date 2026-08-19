/// <summary>
/// Draws the simulation. Reads World state, never mutates it.
/// </summary>
public sealed class WorldRenderer
{
    private const float OutlineThickness = 2.5f;
    private const float PrimaryThickness = 5f;

    public bool ShowDebug = true;

    public void Draw(World world, int stepsLastFrame)
    {
        DrawPlayer(world.Player);
        if (ShowDebug) DrawDebugOverlay(world, stepsLastFrame);
    }

    private void DrawPlayer(Player player)
    {
        Span<Vector2> verts = stackalloc Vector2[Player.MaxSides];
        player.GetVertices(verts);

        int n = player.SideCount;

        for (int i = 0; i < n; i++)
        {
            Vector2 a = verts[i];
            Vector2 b = verts[(i + 1) % n];

            bool isActive = i == player.ActiveSide;
            Raylib.DrawLineEx(a, b,
                isActive ? PrimaryThickness : OutlineThickness,
                isActive ? Color.Orange : Color.DarkBlue);
        }

        // Barrel stub on the active side, showing where shots will originate.
        Vector2 muzzle = player.SideMidpoint(player.ActiveSide);
        Raylib.DrawLineEx(muzzle, muzzle + player.SideNormal(player.ActiveSide) * 14f,
            PrimaryThickness, Color.Orange);

        Raylib.DrawCircleV(player.Position, 3f, Color.DarkBlue);
    }

    private void DrawDebugOverlay(World world, int stepsLastFrame)
    {
        int y = Raylib.GetScreenHeight() - 70;
        Raylib.DrawText($"tick {world.TickCount}", 20, y, 18, Color.Gray);
        Raylib.DrawText($"steps/frame {stepsLastFrame}", 20, y + 22, 18, Color.Gray);
        Raylib.DrawText($"sim {(world.TickCount / (double)World.TickRate):F1}s", 200, y, 18, Color.Gray);
        Raylib.DrawText($"fps {Raylib.GetFPS()}", 200, y + 22, 18, Color.Gray);
        Raylib.DrawText($"sides {world.Player.SideCount}  active {world.Player.ActiveSide}", 340, y, 18, Color.Gray);
    }
}
