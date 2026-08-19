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
        DrawHurtVignette(world.Player);
        DrawHealthBar(world.Player);
        DrawActiveWeapon(world.Player);
        DrawCurrency(world);
    }

    /// <summary>Coins top-right, where it does not compete with the health bar.</summary>
    private void DrawCurrency(World world)
    {
        string coins = $"{world.Coins}";
        int size = 24;
        int w = Raylib.MeasureText(coins, size);
        int x = Raylib.GetScreenWidth() - Margin - w;

        Raylib.DrawText(coins, x, Margin, size, new Color(214, 168, 40, 255));

        const string label = "COINS";
        int lw = Raylib.MeasureText(label, 14);
        Raylib.DrawText(label, Raylib.GetScreenWidth() - Margin - lw, Margin + size + 2, 14,
            new Color(150, 150, 160, 255));
    }

    /// <summary>
    /// Red border pulse when the player takes damage. Screen-space and
    /// exclusive to the player, so it can never be mistaken for enemy hit
    /// feedback no matter how busy the arena is.
    /// </summary>
    private void DrawHurtVignette(Player player)
    {
        float strength = player.HitFlashStrength;
        if (strength <= 0f) return;

        int w = Raylib.GetScreenWidth();
        int h = Raylib.GetScreenHeight();

        int band = (int)(26 + 34 * strength);
        int alpha = (int)(130 * strength);
        var color = new Color(200, 30, 30, alpha);

        // Gradient bands so the edge fades inward instead of ending on a line.
        Raylib.DrawRectangleGradientV(0, 0, w, band, color, Color.Blank);
        Raylib.DrawRectangleGradientV(0, h - band, w, band, Color.Blank, color);
        Raylib.DrawRectangleGradientH(0, 0, band, h, color, Color.Blank);
        Raylib.DrawRectangleGradientH(w - band, 0, band, h, Color.Blank, color);
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
