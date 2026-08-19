/// <summary>
/// The upgrade overlay. Immediate-mode: it draws the panel and performs the
/// interaction in one pass, mutating nothing itself - every purchase goes
/// through Shop, which owns the rules.
/// </summary>
public sealed class ShopRenderer
{
    private const int PanelWidth = 880;
    private const int PanelHeight = 470;
    private const int LeftColumnWidth = 320;
    private const int RowHeight = 62;

    private static readonly Color Panel = new(246, 246, 249, 250);
    private static readonly Color PanelEdge = new(70, 70, 82, 255);
    private static readonly Color Ink = new(45, 45, 55, 255);
    private static readonly Color Muted = new(130, 130, 142, 255);
    private static readonly Color Gold = new(214, 168, 40, 255);
    private static readonly Color Disabled = new(190, 190, 198, 255);

    /// <summary>Which side's detail is shown. Owned here; the sim is untouched.</summary>
    public int SelectedSide;

    public void Draw(World world, Shop shop)
    {
        Rectangle panel = PanelRect();

        DimBackground();

        Raylib.DrawRectangleRec(panel, Panel);
        Raylib.DrawRectangleLinesEx(panel, 2.5f, PanelEdge);

        ClampSelection(world.Player);

        DrawHeader(world, panel);
        DrawSideList(world, panel, shop);
        DrawDetail(world, panel, shop);
        DrawFooter(world, panel, shop);
        DrawFooterHint(panel);
    }

    private void ClampSelection(Player player)
    {
        if (SelectedSide >= player.SideCount || SelectedSide < 0) SelectedSide = 0;
    }

    private static Rectangle PanelRect() => new(
        (Raylib.GetScreenWidth() - PanelWidth) / 2f,
        (Raylib.GetScreenHeight() - PanelHeight) / 2f,
        PanelWidth, PanelHeight);

    private static void DimBackground() =>
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color(15, 15, 25, 140));

    private void DrawHeader(World world, Rectangle panel)
    {
        Raylib.DrawText("UPGRADES", (int)panel.X + 24, (int)panel.Y + 20, 30, Ink);

        string coins = $"{world.Coins}";
        int w = Raylib.MeasureText(coins, 30);
        Raylib.DrawText(coins, (int)(panel.X + panel.Width) - 24 - w, (int)panel.Y + 20, 30, Gold);

        const string label = "COINS";
        int lw = Raylib.MeasureText(label, 13);
        Raylib.DrawText(label, (int)(panel.X + panel.Width) - 24 - lw, (int)panel.Y + 52, 13, Muted);

        var divider = new Rectangle(panel.X + 20, panel.Y + 74, panel.Width - 40, 1.5f);
        Raylib.DrawRectangleRec(divider, new Color(215, 215, 222, 255));
    }

    // --- Left column: turret preview and side list ----------------------

    private void DrawSideList(World world, Rectangle panel, Shop shop)
    {
        Player player = world.Player;

        var centre = new Vector2(panel.X + LeftColumnWidth / 2f, panel.Y + 190f);
        DrawTurretPreview(player, centre);

        int y = (int)panel.Y + 290;

        for (int i = 0; i < player.SideCount; i++)
        {
            var row = new Rectangle(panel.X + 24, y, LeftColumnWidth - 40, 30);

            if (MenuUi.IsHovered(row)) SelectedSide = i;
            if (MenuUi.Clicked(row)) SelectedSide = i;

            bool selected = i == SelectedSide;
            Mount mount = player.Mounts[i];

            if (selected)
            {
                Raylib.DrawRectangleRec(row, new Color(232, 232, 240, 255));
                Raylib.DrawRectangleRec(new Rectangle(row.X, row.Y, 3f, row.Height), Gold);
            }

            string name = mount.IsEmpty ? "(empty)" : mount.Weapon.Name;
            Raylib.DrawText($"{i + 1}. {name}", (int)row.X + 12, (int)row.Y + 6, 19,
                mount.IsEmpty ? Muted : Ink);

            y += 34;
        }
    }

    /// <summary>
    /// Turret drawn with each side in its weapon's colour and the selected side
    /// thickened, so the list and the shape agree at a glance.
    /// </summary>
    private void DrawTurretPreview(Player player, Vector2 centre)
    {
        int n = player.SideCount;
        const float radius = 78f;

        Span<Vector2> verts = stackalloc Vector2[Player.MaxSides];

        float step = MathF.Tau / n;
        float baseRotation = -MathF.PI / 2f - MathF.PI / n;

        for (int i = 0; i < n; i++)
        {
            float a = baseRotation + i * step;
            verts[i] = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }

        for (int i = 0; i < n; i++)
        {
            Vector2 a = verts[i];
            Vector2 b = verts[(i + 1) % n];

            Mount mount = player.Mounts[i];
            Color color = mount.IsEmpty ? Disabled : WeaponColor(mount);

            Raylib.DrawLineEx(a, b, i == SelectedSide ? 6f : 3f, color);

            // Number each side against its edge so the list maps to the shape.
            Vector2 mid = (a + b) * 0.5f;
            Vector2 outward = Vector2.Normalize(mid - centre);
            Vector2 label = mid + outward * 16f;

            string text = (i + 1).ToString();
            int tw = Raylib.MeasureText(text, 16);
            Raylib.DrawText(text, (int)label.X - tw / 2, (int)label.Y - 8, 16,
                i == SelectedSide ? Ink : Muted);
        }
    }

    private static Color WeaponColor(Mount mount)
    {
        uint t = mount.Weapon.Def.PackedTint;
        return new Color(
            (int)((t >> 24) & 0xFF),
            (int)((t >> 16) & 0xFF),
            (int)((t >> 8) & 0xFF),
            255);
    }

    // --- Right column: selected mount detail -----------------------------

    private void DrawDetail(World world, Rectangle panel, Shop shop)
    {
        Player player = world.Player;
        Mount mount = player.Mounts[SelectedSide];

        int x = (int)panel.X + LeftColumnWidth + 20;
        int y = (int)panel.Y + 96;

        string title = mount.IsEmpty
            ? $"SIDE {SelectedSide + 1} - EMPTY"
            : $"SIDE {SelectedSide + 1} - {mount.Weapon.Name.ToUpperInvariant()}";

        Raylib.DrawText(title, x, y, 22, Ink);
        y += 44;

        if (mount.IsEmpty)
        {
            DrawWeaponPicker(world, shop, x, y, panel);
            return;
        }

        DrawUpgradeRow(world, shop, world.UpgradeDefs.Find("damage"), x, y, panel);
        DrawUpgradeRow(world, shop, world.UpgradeDefs.Find("firerate"), x, y + RowHeight, panel);

        DrawCurrentStats(mount, x, y + RowHeight * 2 + 6);
        DrawWeaponPicker(world, shop, x, y + RowHeight * 2 + 34, panel);
    }

    /// <summary>
    /// Weapon list built from the loaded catalogue, so a weapon added to
    /// weapons.json is purchasable with no code change here.
    /// </summary>
    private void DrawWeaponPicker(World world, Shop shop, int x, int y, Rectangle panel)
    {
        UpgradeDef equip = world.UpgradeDefs.Find("equip");
        if (equip == null) return;

        Mount mount = world.Player.Mounts[SelectedSide];
        int right = (int)(panel.X + panel.Width) - 30;

        string heading = mount.IsEmpty ? "FIT A WEAPON" : "REPLACE WEAPON";
        Raylib.DrawText(heading, x, y, 15, Muted);

        int cost = shop.CostOf(equip, SelectedSide);
        int rowY = y + 24;

        foreach (WeaponDef def in world.Weapons.Weapons)
        {
            bool isFitted = !mount.IsEmpty && mount.Weapon.Def.Id == def.Id;
            bool affordable = world.Coins >= cost;
            bool clickable = !isFitted && affordable;

            var row = new Rectangle(x - 8, rowY - 4, right - x + 16, 28);
            bool hovered = MenuUi.IsHovered(row);

            if (hovered && clickable)
            {
                Raylib.DrawRectangleRec(row, new Color(236, 240, 232, 255));
                Raylib.DrawRectangleLinesEx(row, 1.5f, new Color(120, 170, 110, 255));
            }

            if (clickable && MenuUi.Clicked(row)) shop.BuyEquip(equip, SelectedSide, def);

            // Colour swatch, matching the bullet and turret colour for this weapon.
            Raylib.DrawRectangleRec(new Rectangle(x, rowY + 2, 12f, 12f), TintOf(def));

            Raylib.DrawText(def.Name, x + 22, rowY, 17,
                isFitted ? Muted : clickable ? Ink : Disabled);

            string trailing = isFitted ? "fitted" : $"{cost}c";
            int tw = Raylib.MeasureText(trailing, 15);
            Raylib.DrawText(trailing, right - tw, rowY + 1, 15,
                isFitted ? Muted : affordable ? Gold : Disabled);

            rowY += 30;
        }

        if (!mount.IsEmpty)
        {
            Raylib.DrawText("Replacing resets this side's upgrade levels.", x, rowY + 2, 14, Muted);
        }
    }

    private static Color TintOf(WeaponDef def)
    {
        uint t = def.PackedTint;
        return new Color(
            (int)((t >> 24) & 0xFF),
            (int)((t >> 16) & 0xFF),
            (int)((t >> 8) & 0xFF),
            255);
    }

    /// <summary>
    /// One purchasable row. Clicking anywhere on it buys, so the whole row is
    /// the target rather than a small button.
    /// </summary>
    private void DrawUpgradeRow(World world, Shop shop, UpgradeDef def, int x, int y, Rectangle panel)
    {
        if (def == null) return;

        int right = (int)(panel.X + panel.Width) - 30;
        var row = new Rectangle(x - 8, y - 6, right - x + 16, RowHeight - 8);

        int level = shop.LevelOf(def, SelectedSide);
        bool maxed = shop.IsMaxed(def, SelectedSide);
        bool affordable = shop.CanAfford(def, SelectedSide);
        bool buyable = shop.CanBuy(def, SelectedSide);

        bool hovered = MenuUi.IsHovered(row);

        if (hovered && buyable)
        {
            Raylib.DrawRectangleRec(row, new Color(236, 240, 232, 255));
            Raylib.DrawRectangleLinesEx(row, 1.5f, new Color(120, 170, 110, 255));
        }
        else if (hovered)
        {
            Raylib.DrawRectangleRec(row, new Color(244, 238, 238, 255));
        }

        if (buyable && MenuUi.Clicked(row)) shop.Buy(def, SelectedSide);

        Raylib.DrawText(def.Name, x, y, 19, maxed ? Muted : Ink);

        string levelText = maxed ? $"Lv {level}  MAX" : $"Lv {level} -> {level + 1}";
        Raylib.DrawText(levelText, x + 150, y, 18, Muted);

        string cost = maxed ? "-" : $"{def.CostFor(level)}c";
        int cw = Raylib.MeasureText(cost, 19);
        Raylib.DrawText(cost, right - cw, y,
            19, maxed ? Muted : affordable ? Gold : Disabled);

        DrawLevelBar(def, level, x, y + 26, right - x);
    }

    /// <summary>
    /// Resolved values for the fitted weapon, so the effect of a purchase is
    /// visible immediately rather than only in play.
    /// </summary>
    private static void DrawCurrentStats(Mount mount, int x, int y)
    {
        if (mount.IsEmpty) return;

        StatBlock stats = mount.Weapon.Stats;

        Raylib.DrawText(
            $"damage {stats.Get(StatId.Damage):0.#}    " +
            $"rate {stats.Get(StatId.FireRate):0.##}/s    " +
            $"speed {stats.Get(StatId.BulletSpeed):0}",
            x, y, 16, Muted);
    }

    private static void DrawLevelBar(UpgradeDef def, int level, int x, int y, int width)
    {
        int segments = def.MaxLevel > 0 ? def.MaxLevel : 10;
        float segWidth = width / (float)segments;

        for (int i = 0; i < segments; i++)
        {
            var seg = new Rectangle(x + i * segWidth, y, segWidth - 3f, 8f);
            Raylib.DrawRectangleRec(seg, i < level ? Gold : new Color(224, 224, 230, 255));
        }
    }

    /// <summary>
    /// Purchases that affect the whole run rather than one mount: another side,
    /// more max health, and a repair. Kept along the bottom so they read as
    /// global, not as part of the selected side's detail.
    /// </summary>
    private void DrawFooter(World world, Rectangle panel, Shop shop)
    {
        int y = (int)(panel.Y + panel.Height) - 84;
        float width = (panel.Width - 60) / 3f;

        DrawFooterButton(world, shop, world.UpgradeDefs.Find("shape"),
            new Rectangle(panel.X + 24, y, width - 10, 42),
            ShapeLabel(world.Player));

        DrawFooterButton(world, shop, world.UpgradeDefs.Find("maxhealth"),
            new Rectangle(panel.X + 24 + width, y, width - 10, 42),
            MaxHealthLabel(world.Player));

        DrawFooterButton(world, shop, world.UpgradeDefs.Find("repair"),
            new Rectangle(panel.X + 24 + width * 2, y, width - 10, 42),
            RepairLabel(world.Player, world.UpgradeDefs.Find("repair")));
    }

    private static string MaxHealthLabel(Player player) =>
        $"Max Health ({player.MaxHealth:0})";

    /// <summary>
    /// Names the amount restored, and says plainly when it is unavailable -
    /// a greyed button with no reason reads as broken rather than disabled.
    /// </summary>
    private static string RepairLabel(Player player, UpgradeDef def)
    {
        if (player.Health >= player.MaxHealth) return "Repair (at full)";

        float missing = player.MaxHealth - player.Health;
        float restored = def == null ? 0f : MathF.Min(def.ValuePerLevel, missing);

        return $"Repair (+{restored:0})";
    }

    /// <summary>Names the shape being bought, so the cost has something concrete attached.</summary>
    private static string ShapeLabel(Player player)
    {
        string next = player.SideCount switch
        {
            3 => "Square",
            4 => "Pentagon",
            5 => "Hexagon",
            6 => "Heptagon",
            7 => "Octagon",
            _ => $"{player.SideCount + 1} sides",
        };

        return player.SideCount >= Player.MaxSides ? "Max sides" : $"Add Side ({next})";
    }

    private void DrawFooterButton(World world, Shop shop, UpgradeDef def, Rectangle rect, string labelOverride)
    {
        if (def == null) return;

        bool buyable = shop.CanBuy(def, SelectedSide);
        bool maxed = shop.IsMaxed(def, SelectedSide);
        bool hovered = MenuUi.IsHovered(rect);

        Raylib.DrawRectangleRec(rect, buyable && hovered
            ? new Color(236, 240, 232, 255)
            : new Color(238, 238, 244, 255));

        Raylib.DrawRectangleLinesEx(rect, 1.5f,
            buyable ? new Color(120, 170, 110, 255) : new Color(214, 214, 222, 255));

        if (buyable && MenuUi.Clicked(rect)) shop.Buy(def, SelectedSide);

        string label = labelOverride ?? def.Name;
        Raylib.DrawText(label, (int)rect.X + 12, (int)rect.Y + 7, 17,
            buyable ? Ink : Muted);

        int level = shop.LevelOf(def, SelectedSide);
        string cost = maxed ? "MAX" : $"{def.CostFor(level)}c";
        int cw = Raylib.MeasureText(cost, 16);

        Raylib.DrawText(cost, (int)(rect.X + rect.Width) - 12 - cw, (int)rect.Y + 22, 16,
            maxed ? Muted : buyable ? Gold : Disabled);
    }

    private static void DrawFooterHint(Rectangle panel)
    {
        const string hint = "Right-click or ESC to close";
        int w = Raylib.MeasureText(hint, 15);
        Raylib.DrawText(hint,
            (int)(panel.X + panel.Width / 2) - w / 2,
            (int)(panel.Y + panel.Height) - 30, 15, Muted);
    }
}
