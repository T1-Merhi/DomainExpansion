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
        // Shake is applied to the whole world layer via a camera offset, so it
        // never touches entity positions and cannot affect collision. The HUD
        // draws outside this block and therefore stays perfectly still.
        var camera = new Camera2D
        {
            Target = Vector2.Zero,
            Offset = ShakeOffset(world),
            Rotation = 0f,
            Zoom = 1f,
        };

        Raylib.BeginMode2D(camera);

        DrawExplosions(world);
        DrawEnemies(world);
        DrawBullets(world);
        DrawPlayer(world.Player);
        DrawFloatingTexts(world);

        Raylib.EndMode2D();

        if (ShowDebug) DrawDebugOverlay(world, stepsLastFrame);
    }

    /// <summary>
    /// Pseudo-random offset that changes every frame, so the shake reads as a
    /// jolt rather than a smooth slide.
    /// </summary>
    private static Vector2 ShakeOffset(World world)
    {
        float amount = world.ShakeAmount;
        if (amount <= 0f) return Vector2.Zero;

        double t = Raylib.GetTime() * 60.0;

        return new Vector2(
            (float)Math.Sin(t * 12.9898) * amount,
            (float)Math.Cos(t * 7.233) * amount);
    }

    private void DrawFloatingTexts(World world)
    {
        for (int i = 0; i < world.FloatingTexts.ActiveCount; i++)
        {
            FloatingText f = world.FloatingTexts[i];

            float t = f.Progress;

            // Hold full opacity for the first half, then fade - fading from the
            // start makes small numbers unreadable before they register.
            byte alpha = t < 0.5f ? (byte)255 : (byte)(255 * (1f - (t - 0.5f) * 2f));

            bool isCoin = f.Kind == FloatingTextKind.Coin;

            string text = isCoin
                ? "+" + MathF.Round(f.Amount).ToString("0")
                : MathF.Round(f.Amount).ToString("0");

            int size = isCoin ? 20 : 18;
            int w = Raylib.MeasureText(text, size);

            Color tint = isCoin
                ? new Color(255, 205, 60, (int)alpha)     // gold for currency
                : new Color(255, 240, 120, (int)alpha);   // pale for damage

            var shadow = new Color(0, 0, 0, (int)(alpha * 0.45f));
            Raylib.DrawText(text, (int)f.Position.X - w / 2 + 1, (int)f.Position.Y + 1, size, shadow);
            Raylib.DrawText(text, (int)f.Position.X - w / 2, (int)f.Position.Y, size, tint);
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
    private static Vector2 HitShakeOffset(Enemy enemy)
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
            Vector2 at = e.Position + HitShakeOffset(e);

            // Shape and colour per the spec: red square, purple diamond,
            // orange pentagon - so type is identifiable at a glance.
            switch (e.Type)
            {
                case EnemyType.Chaser:
                    Raylib.DrawRectangleV(
                        at - new Vector2(e.Radius, e.Radius),
                        new Vector2(e.Radius * 2f, e.Radius * 2f),
                        HitFlash(e, EnemyColor(world, e)));
                    break;

                case EnemyType.Shooter:
                    Raylib.DrawPoly(at, 4, e.Radius, 0f, HitFlash(e, EnemyColor(world, e)));
                    break;

                case EnemyType.Spawner:
                    Raylib.DrawPoly(at, 5, e.Radius, 0f, HitFlash(e, EnemyColor(world, e)));
                    DrawSpawnTelegraph(at, e);
                    break;
            }
        }
    }

    /// <summary>
    /// Body colour resolved on spawn and carried on the entity. Looking it up
    /// here meant a linear catalog scan plus an enum parse for every enemy on
    /// every frame.
    /// </summary>
    private static Color EnemyColor(World world, Enemy enemy) => FromPacked(enemy.Tint);

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
        Vector2 normal = player.SideNormal(player.ActiveSide);

        Raylib.DrawLineEx(muzzle, muzzle + normal * 10f, PrimaryThickness + 1f, activeColor);

        if (player.MuzzleFlashTicks > 0)
        {
            float strength = player.MuzzleFlashTicks / (float)Player.MuzzleFlashDuration;
            Vector2 tip = muzzle + normal * 12f;

            Raylib.DrawCircleV(tip, 4f + 7f * strength,
                new Color(255, 236, 170, (int)(220 * strength)));
        }

        DrawMountCore(player, activeColor);

        if (hurt > 0f) DrawHitRing(player, hurt);
    }

    /// <summary>
    /// The equipped weapon's initial at the turret centre - R, S, P - sized to
    /// sit inside the polygon. Drawn upright rather than rotating with the
    /// turret, since a spinning letter is unreadable.
    /// </summary>
    private static void DrawMountCore(Player player, Color color)
    {
        Mount mount = player.ActiveMount;
        string glyph = mount.IsEmpty ? "-" : mount.Weapon.Name.Substring(0, 1).ToUpperInvariant();

        // Scale to the turret so it stays fitted if the radius ever changes.
        int size = (int)Math.Clamp(player.Radius * 0.62f, 10f, 20f);

        int w = Raylib.MeasureText(glyph, size);
        int x = (int)player.Position.X - w / 2;
        int y = (int)player.Position.Y - size / 2;

        // Dark offset copy first, so the letter stays legible against bullets
        // and enemies passing underneath.
        Raylib.DrawText(glyph, x + 1, y + 1, size, new Color(25, 25, 30, 180));
        Raylib.DrawText(glyph, x, y, size, color);
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
        Raylib.DrawText($"wave {world.WaveNumber} {world.WaveRunner.Phase}  hp x{world.Waves.ScaleOf("MaxHealth", Math.Max(1, world.WaveNumber)):0.00}",
            760, y + 22, 18, Color.Gray);
        Raylib.DrawText($"floats {world.FloatingTexts.ActiveCount}/{world.FloatingTexts.Capacity}", 760, y, 18, Color.Gray);
    }
}
