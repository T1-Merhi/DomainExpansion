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
        DrawExplosions(world);
        DrawEnemies(world);
        DrawBullets(world);
        DrawPlayer(world.Player);
        if (ShowDebug) DrawDebugOverlay(world, stepsLastFrame);
    }

    private void DrawExplosions(World world)
    {
        for (int i = 0; i < world.Explosions.ActiveCount; i++)
        {
            Explosion e = world.Explosions[i];

            // Expands to full radius while fading, so the blast reads as an
            // event rather than a static disc.
            float t = e.Progress;
            float radius = e.Radius * (0.4f + 0.6f * t);
            var color = new Color(255, 140, 40, (int)(200 * (1f - t)));

            Raylib.DrawCircleV(e.Position, radius, color);
        }
    }

    private void DrawEnemies(World world)
    {
        for (int i = 0; i < world.Enemies.ActiveCount; i++)
        {
            Enemy e = world.Enemies[i];

            // Shape and colour per the spec: red square, purple diamond,
            // orange pentagon - so type is identifiable at a glance.
            switch (e.Type)
            {
                case EnemyType.Chaser:
                    Raylib.DrawRectangleV(
                        e.Position - new Vector2(e.Radius, e.Radius),
                        new Vector2(e.Radius * 2f, e.Radius * 2f),
                        Color.Red);
                    break;

                case EnemyType.Shooter:
                    Raylib.DrawPoly(e.Position, 4, e.Radius, 0f, Color.Purple);
                    break;

                case EnemyType.Spawner:
                    Raylib.DrawPoly(e.Position, 5, e.Radius, 0f, Color.Orange);
                    break;
            }
        }
    }

    private void DrawBullets(World world)
    {
        for (int i = 0; i < world.PlayerBullets.ActiveCount; i++)
        {
            Bullet b = world.PlayerBullets[i];

            // Explosive rounds read differently so the pistol is identifiable.
            Color color = b.ExplosionRadius > 0f ? Color.Red : Color.DarkBlue;
            Raylib.DrawCircleV(b.Position, b.Radius, color);
        }

        // Enemy fire is drawn as an outlined ring so it never reads as the
        // player's own bullets in a crowded screen.
        for (int i = 0; i < world.EnemyBullets.ActiveCount; i++)
        {
            Bullet b = world.EnemyBullets[i];
            Raylib.DrawCircleV(b.Position, b.Radius, Color.Purple);
            Raylib.DrawCircleLinesV(b.Position, b.Radius + 2f, Color.Magenta);
        }
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

        // Side index just inside each edge, so the carousel is readable.
        for (int i = 0; i < n; i++)
        {
            Vector2 label = player.Position + (player.SideMidpoint(i) - player.Position) * 0.55f;
            string text = i.ToString();
            int w = Raylib.MeasureText(text, 14);
            Raylib.DrawText(text, (int)label.X - w / 2, (int)label.Y - 7, 14,
                i == player.ActiveSide ? Color.Orange : Color.Gray);
        }

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
        Raylib.DrawText($"bullets {world.PlayerBullets.ActiveCount}/{world.PlayerBullets.Capacity}", 340, y + 22, 18, Color.Gray);
        Raylib.DrawText($"enemies {world.Enemies.ActiveCount}/{world.Enemies.Capacity}", 560, y, 18, Color.Gray);
    }
}
