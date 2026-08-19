/// <summary>
/// Screen-space overlay: health, active weapon, and later score and coins.
/// Separate from WorldRenderer because none of it lives in world space.
/// </summary>
public sealed class HudRenderer
{
    private const int BarWidth = 260;
    private const int BarHeight = 22;
    private const int Margin = 20;

    public void Draw(World world)
    {
        DrawHealthBar(world.Player);
        DrawActiveWeapon(world.Player);
    }

    private void DrawHealthBar(Player player)
    {
        int y = Raylib.GetScreenHeight() - Margin - BarHeight;

        var back = new Rectangle(Margin, y, BarWidth, BarHeight);
        Raylib.DrawRectangleRec(back, new Color(210, 210, 216, 255));

        float fraction = player.HealthFraction;
        if (fraction > 0f)
        {
            var fill = new Rectangle(Margin, y, BarWidth * fraction, BarHeight);

            // Colour tracks remaining health so low HP is obvious peripherally.
            Color color = fraction > 0.5f ? Color.Lime
                        : fraction > 0.25f ? Color.Orange
                        : Color.Red;

            Raylib.DrawRectangleRec(fill, color);
        }

        Raylib.DrawRectangleLinesEx(back, 2f, new Color(90, 90, 100, 255));

        string label = $"{MathF.Ceiling(player.Health)} / {MathF.Round(player.MaxHealth)}";
        Raylib.DrawText(label, Margin + 8, y + 3, 18, new Color(30, 30, 35, 255));
    }

    private void DrawActiveWeapon(Player player)
    {
        Mount active = player.ActiveMount;
        string text = active.IsEmpty ? "(empty mount)" : active.Weapon.Name;

        int y = Raylib.GetScreenHeight() - Margin - BarHeight - 28;
        Raylib.DrawText($"side {player.ActiveSide}: {text}", Margin, y, 20,
            active.IsEmpty ? Color.Gray : Color.Orange);
    }
}
