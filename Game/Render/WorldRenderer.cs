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
        DrawDamageNumbers(world);
        if (ShowDebug) DrawDebugOverlay(world, stepsLastFrame);
    }

    private void DrawDamageNumbers(World world)
    {
        for (int i = 0; i < world.DamageNumbers.ActiveCount; i++)
        {
            DamageNumber d = world.DamageNumbers[i];

            float t = d.Progress;

            // Hold full opacity for the first half, then fade - fading from the
            // start makes small numbers unreadable before they register.
            byte alpha = t < 0.5f ? (byte)255 : (byte)(255 * (1f - (t - 0.5f) * 2f));

            string text = MathF.Round(d.Amount).ToString("0");
            int size = 18;
            int w = Raylib.MeasureText(text, size);

            var shadow = new Color(0, 0, 0, (int)(alpha * 0.45f));
            Raylib.DrawText(text, (int)d.Position.X - w / 2 + 1, (int)d.Position.Y + 1, size, shadow);
            Raylib.DrawText(text, (int)d.Position.X - w / 2, (int)d.Position.Y, size,
                new Color(255, 240, 120, (int)alpha));
        }
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

            Color color = FromPacked(e.Tint);
            color.A = (byte)(200 * (1f - t));

            Raylib.DrawCircleV(e.Position, radius, color);
        }
    }

    /// <summary>
    /// Hit reaction: a decaying jitter applied at draw time only, so the shake
    /// never moves the enemy's actual position and cannot affect collision.
    /// The offset alternates each tick, which reads as a shudder rather than
    /// a slide.
    /// </summary>
    private static Vector2 ShakeOffset(Enemy enemy)
    {
        if (enemy.HitShakeTicks <= 0) return Vector2.Zero;

        float decay = enemy.HitShakeTicks / (float)Enemy.HitShakeDuration;
        float amplitude = 4.5f * decay;

        // Flip direction on alternate ticks; the second axis uses a different
        // multiplier so it does not shake along a single diagonal.
        float sign = (enemy.HitShakeTicks & 1) == 0 ? 1f : -1f;

        return new Vector2(sign * amplitude, sign * amplitude * 0.55f);
    }

    /// <summary>
    /// Blends the enemy's colour toward white for the first few ticks after a
    /// hit. Shorter than the shake, so the flash reads as the impact itself
    /// while the shake carries the reaction.
    /// </summary>
    private static Color HitFlash(Enemy enemy, Color baseColor)
    {
        const int FlashTicks = 3;

        if (enemy.HitShakeTicks <= Enemy.HitShakeDuration - FlashTicks) return baseColor;

        return new Color(
            (int)(baseColor.R + (255 - baseColor.R) * 0.75f),
            (int)(baseColor.G + (255 - baseColor.G) * 0.75f),
            (int)(baseColor.B + (255 - baseColor.B) * 0.75f),
            (int)baseColor.A);
    }

    /// <summary>
    /// Ring that contracts onto the spawner as its next emission approaches.
    /// Derived from ActionCooldown, so it needs no extra simulation state, and
    /// it warns before the beat rather than reporting it afterwards.
    /// </summary>
    private static void DrawSpawnTelegraph(Vector2 at, Enemy enemy)
    {
        if (enemy.ActionCooldown <= 0 || enemy.ActionCooldown > SpawnerBehavior.TelegraphTicks) return;

        float t = enemy.ActionCooldown / (float)SpawnerBehavior.TelegraphTicks;
        float radius = enemy.Radius + 4f + 26f * t;
        var color = new Color(255, 165, 0, (int)(60 + 160 * (1f - t)));

        Raylib.DrawCircleLinesV(at, radius, color);
    }

    private void DrawEnemies(World world)
    {
        for (int i = 0; i < world.Enemies.ActiveCount; i++)
        {
            Enemy e = world.Enemies[i];
            Vector2 at = e.Position + ShakeOffset(e);

            // Shape and colour per the spec: red square, purple diamond,
            // orange pentagon - so type is identifiable at a glance.
            switch (e.Type)
            {
                case EnemyType.Chaser:
                    Raylib.DrawRectangleV(
                        at - new Vector2(e.Radius, e.Radius),
                        new Vector2(e.Radius * 2f, e.Radius * 2f),
                        HitFlash(e, Color.Red));
                    break;

                case EnemyType.Shooter:
                    Raylib.DrawPoly(at, 4, e.Radius, 0f, HitFlash(e, Color.Purple));
                    break;

                case EnemyType.Spawner:
                    Raylib.DrawPoly(at, 5, e.Radius, 0f, HitFlash(e, Color.Orange));
                    DrawSpawnTelegraph(at, e);
                    break;
            }
        }
    }

    /// <summary>Unpacks the sim's opaque RGBA value into a drawable colour.</summary>
    private static Color FromPacked(uint tint) => new(
        (int)((tint >> 24) & 0xFF),
        (int)((tint >> 16) & 0xFF),
        (int)((tint >> 8) & 0xFF),
        (int)(tint & 0xFF));

    private void DrawBullets(World world)
    {
        // Colour comes from the weapon that fired it, so weapon type is
        // identifiable from the projectiles alone.
        for (int i = 0; i < world.PlayerBullets.ActiveCount; i++)
        {
            Bullet b = world.PlayerBullets[i];
            Raylib.DrawCircleV(b.Position, b.Radius, FromPacked(b.Tint));
        }

        // Enemy fire keeps an outlined ring so it never reads as the player's
        // own bullets in a crowded screen, regardless of tint.
        for (int i = 0; i < world.EnemyBullets.ActiveCount; i++)
        {
            Bullet b = world.EnemyBullets[i];
            Raylib.DrawCircleV(b.Position, b.Radius, FromPacked(b.Tint));
            Raylib.DrawCircleLinesV(b.Position, b.Radius + 2f, Color.Magenta);
        }
    }

    private static readonly Color EmptyMountColor = new(150, 150, 160, 255);

    /// <summary>A mount's weapon colour, or grey when the slot is unfilled.</summary>
    private static Color MountColor(Mount mount) =>
        mount.IsEmpty ? EmptyMountColor : FromPacked(mount.Weapon.Def.PackedTint);

    /// <summary>Blends toward red while the player is reacting to a hit.</summary>
    private static Color Hurt(Color color, float strength)
    {
        if (strength <= 0f) return color;

        return new Color(
            (int)(color.R + (230 - color.R) * strength),
            (int)(color.G * (1f - strength)),
            (int)(color.B * (1f - strength)),
            (int)color.A);
    }

    private void DrawPlayer(Player player)
    {
        Span<Vector2> verts = stackalloc Vector2[Player.MaxSides];
        player.GetVertices(verts);

        int n = player.SideCount;
        float hurt = player.HitFlashStrength;

        // Each side is drawn in the colour of the weapon mounted on it, so the
        // whole loadout is legible from the turret without any UI.
        for (int i = 0; i < n; i++)
        {
            Vector2 a = verts[i];
            Vector2 b = verts[(i + 1) % n];

            bool isActive = i == player.ActiveSide;

            Raylib.DrawLineEx(a, b,
                isActive ? PrimaryThickness : OutlineThickness,
                Hurt(MountColor(player.Mounts[i]), hurt));
        }

        Mount active = player.ActiveMount;
        Color activeColor = Hurt(MountColor(active), hurt);

        // Barrel stub on the active side, showing where shots will originate.
        Vector2 muzzle = player.SideMidpoint(player.ActiveSide);
        Raylib.DrawLineEx(muzzle, muzzle + player.SideNormal(player.ActiveSide) * 10f,
            PrimaryThickness + 1f, activeColor);

        DrawMountCore(player, activeColor);

        if (hurt > 0f) DrawHitRing(player, hurt);
    }

    /// <summary>
    /// The equipment mount at the centre: a small polygon matching the turret's
    /// shape, filled with the active weapon's colour. Replaces the old plain
    /// dot, which carried no information.
    /// </summary>
    private static void DrawMountCore(Player player, Color color)
    {
        float rotationDeg = player.Rotation * 180f / MathF.PI;
        float radius = MathF.Max(4.5f, player.Radius * 0.32f);

        Raylib.DrawPoly(player.Position, player.SideCount, radius, rotationDeg, color);
        Raylib.DrawPolyLines(player.Position, player.SideCount, radius + 1.5f, rotationDeg,
            new Color(40, 40, 50, 200));
    }

    /// <summary>
    /// Ring expanding outward from the player on damage. Enemies flash white
    /// and shudder in place; the player flashes red and emits a ring, so the
    /// two are never confused in a crowded fight.
    /// </summary>
    private static void DrawHitRing(Player player, float strength)
    {
        float radius = player.Radius + (1f - strength) * 26f;
        var color = new Color(235, 60, 50, (int)(190 * strength));

        Raylib.DrawCircleLinesV(player.Position, radius, color);
        Raylib.DrawCircleLinesV(player.Position, radius + 2f, color);
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
        Raylib.DrawText($"enemy shots {world.EnemyBullets.ActiveCount}", 560, y + 22, 18, Color.Gray);
        Raylib.DrawText($"dmg numbers {world.DamageNumbers.ActiveCount}/{world.DamageNumbers.Capacity}", 760, y, 18, Color.Gray);
    }
}
