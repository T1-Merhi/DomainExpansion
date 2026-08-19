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
        DrawWaveStatus(world);
    }

    /// <summary>
    /// Wave number top-centre, with the rest countdown beneath it. Centred
    /// because it is the thing the player looks up for between waves, and the
    /// corners are already taken by health and currency.
    /// </summary>
    private void DrawWaveStatus(World world)
    {
        WaveRunner runner = world.WaveRunner;
        int centreX = Raylib.GetScreenWidth() / 2;

        string wave = runner.WaveNumber <= 0 ? "GET READY" : $"WAVE {runner.WaveNumber}";
        int ww = Raylib.MeasureText(wave, 26);
        Raylib.DrawText(wave, centreX - ww / 2, Margin, 26, new Color(60, 60, 70, 255));

        if (runner.Phase == WavePhase.Resting)
        {
            // Ceiling, so it reads 20 the instant the rest begins and only
            // shows 0 when the wave actually starts.
            int seconds = (int)MathF.Ceiling(runner.RestSecondsRemaining);

            string countdown = $"next wave in {seconds}s   -   ENTER to start now";
            int cw = Raylib.MeasureText(countdown, 17);
            Raylib.DrawText(countdown, centreX - cw / 2, Margin + 32, 17, new Color(150, 150, 160, 255));
        }
        else
        {
            int remaining = runner.RemainingToSpawn;
            string status = remaining > 0
                ? $"{world.Enemies.ActiveCount} active   {remaining} incoming"
                : $"{world.Enemies.ActiveCount} remaining";

            int sw = Raylib.MeasureText(status, 17);
            Raylib.DrawText(status, centreX - sw / 2, Margin + 32, 17, new Color(150, 150, 160, 255));
        }
    }

    /// <summary>
    /// Score and coins top-right. Kept visually distinct - score in neutral
    /// grey, coins in gold - because one is spent and the other never is, and
    /// confusing them would make purchase decisions read wrongly.
    /// </summary>
    private void DrawCurrency(World world)
    {
        int right = Raylib.GetScreenWidth() - Margin;

        int y = DrawRightAligned($"{world.Score}", "SCORE", right, Margin,
            28, new Color(60, 60, 70, 255));

        DrawRightAligned($"{world.Coins}", "COINS", right, y + 10,
            24, new Color(214, 168, 40, 255));
    }

    /// <summary>Draws a right-aligned value with a caption; returns the y below it.</summary>
    private static int DrawRightAligned(string value, string caption, int right, int y, int size, Color color)
    {
        int vw = Raylib.MeasureText(value, size);
        Raylib.DrawText(value, right - vw, y, size, color);

        const int captionSize = 13;
        int cw = Raylib.MeasureText(caption, captionSize);
        Raylib.DrawText(caption, right - cw, y + size + 1, captionSize, new Color(150, 150, 160, 255));

        return y + size + captionSize + 2;
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
