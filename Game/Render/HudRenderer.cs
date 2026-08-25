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
        DrawDashAndShield(world);
        DrawConfigStamp();
    }

    /// <summary>
    /// Admin-only config stamp. The whole point of the two-instance workflow is
    /// confirming a reload actually happened, which needs a visible version
    /// that changes. Absent in player mode.
    /// </summary>
    private void DrawConfigStamp()
    {
        if (!AppMode.IsAdmin) return;

        ConfigStore store = ConfigStore.Current;

        string text = $"config v{store.Version}  {store.LoadedAt:HH:mm:ss}";
        int y = Raylib.GetScreenHeight() - Margin - 76;

        Raylib.DrawText(text, Margin, y, 14, new Color(120, 120, 132, 255));

        if (!store.HasWarning) return;

        Raylib.DrawText($"config warning: {store.LastWarning}", Margin, y - 18, 14,
            new Color(206, 122, 20, 255));
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
    /// Dash readiness and shield strength, above the health bar. Both are
    /// resources spent under pressure, so they sit together where the player is
    /// already looking for their health.
    /// </summary>
    private void DrawDashAndShield(World world)
    {
        int y = Raylib.GetScreenHeight() - Margin - BarHeight - 54;

        // Fills as the cooldown drains, so "ready" is a full bar.
        float ready = 1f - world.Player.DashCooldownFraction;

        Raylib.DrawRectangleRec(new Rectangle(Margin, y, 90, 8), new Color(210, 210, 216, 255));
        Raylib.DrawRectangleRec(new Rectangle(Margin, y, 90 * ready, 8),
            world.Player.DashReady ? new Color(90, 180, 220, 255) : new Color(150, 160, 170, 255));

        Raylib.DrawText("DASH", Margin + 98, y - 4, 14,
            world.Player.DashReady ? new Color(60, 60, 70, 255) : new Color(150, 150, 160, 255));

        if (!world.Shield.Enabled) return;

        // One pip per arc, so a broken arc registers without looking away from
        // the fight to inspect the ring itself.
        int px = Margin;
        int py = y - 18;

        for (int i = 0; i < Shield.ArcCount; i++)
        {
            float fraction = Math.Clamp(
                world.Shield.HealthOf(i) / MathF.Max(1f, world.Shield.MaxArcHealth), 0f, 1f);

            Raylib.DrawRectangleRec(new Rectangle(px, py, 28, 8), new Color(210, 210, 216, 255));

            if (fraction > 0f)
            {
                Raylib.DrawRectangleRec(new Rectangle(px, py, 28 * fraction, 8),
                    new Color(90, 180, 235, 255));
            }

            px += 32;
        }

        Raylib.DrawText("SHIELD", Margin + 98, py - 4, 14, new Color(150, 150, 160, 255));
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
        // 1-based, matching the shop - two numbering schemes for the same
        // sides is a needless translation step for the player.
        Raylib.DrawText($"side {player.ActiveSide + 1}: {text}", Margin, y, 20,
            active.IsEmpty ? Color.Gray : Color.Orange);
    }
}
