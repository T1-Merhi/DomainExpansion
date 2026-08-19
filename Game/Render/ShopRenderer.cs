/// <summary>
/// The upgrade overlay. Immediate-mode: it draws the panel and performs the
/// interaction in one pass, mutating nothing itself - every purchase goes
/// through Shop, which owns the rules.
/// </summary>
public sealed class ShopRenderer
{
    private const int PanelWidth = 880;
    private const int PanelHeight = 560;
    private const int LeftColumnWidth = 320;
    private const int RowHeight = 56;
    private const int PickerRowHeight = 28;

    private static readonly Color Panel = new(246, 246, 249, 250);
    private static readonly Color PanelEdge = new(70, 70, 82, 255);
    private static readonly Color Ink = new(45, 45, 55, 255);
    private static readonly Color Muted = new(130, 130, 142, 255);
    private static readonly Color Gold = new(214, 168, 40, 255);
    private static readonly Color Maxed = new(56, 158, 74, 255);
    private static readonly Color Disabled = new(190, 190, 198, 255);
    private static readonly Color HoverFill = new(236, 240, 232, 255);
    private static readonly Color HoverEdge = new(120, 170, 110, 255);

    /// <summary>Which side's detail is shown. Owned here; the sim is untouched.</summary>
    public int SelectedSide;

    /// <summary>Reused so building the upgrade row list does not allocate per frame.</summary>
    private readonly List<UpgradeDef> _rows = new();

    public void Draw(World world, Shop shop)
    {
        Rectangle panel = PanelRect();

        DimBackground();

        Raylib.DrawRectangleRec(panel, Panel);
        Raylib.DrawRectangleLinesEx(panel, 2.5f, PanelEdge);

        ClampSelection(world.Player);
        HandleSideSelection(world.Player);

        DrawHeader(world, panel);
        DrawSidePanel(world, panel);
        DrawDetail(world, panel, shop);
        DrawFooter(world, panel, shop);
        DrawFooterHint(panel);
    }

    private void ClampSelection(Player player)
    {
        if (SelectedSide >= player.SideCount || SelectedSide < 0) SelectedSide = 0;
    }

    /// <summary>
    /// Side switching matches the carousel in play: scroll to change side. Small
    /// per-side hit targets were fiddly to click and grew into the footer once
    /// the polygon had many sides, so the list they replaced is gone entirely.
    /// </summary>
    private void HandleSideSelection(Player player)
    {
        int n = player.SideCount;
        if (n <= 0) return;

        int delta = (int)Raylib.GetMouseWheelMove();

        if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.Down)) delta += 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.Up)) delta -= 1;

        if (delta == 0) return;

        SelectedSide = ((SelectedSide + delta) % n + n) % n;
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

    // --- Left column: turret preview -------------------------------------

    private void DrawSidePanel(World world, Rectangle panel)
    {
        Player player = world.Player;

        var centre = new Vector2(panel.X + LeftColumnWidth / 2f, panel.Y + 210f);
        DrawTurretPreview(player, centre);

        Mount mount = player.Mounts[SelectedSide];
        int centreX = (int)centre.X;

        string title = $"SIDE {SelectedSide + 1} / {player.SideCount}";
        int tw = Raylib.MeasureText(title, 20);
        Raylib.DrawText(title, centreX - tw / 2, (int)panel.Y + 330, 20, Ink);

        string name = mount.IsEmpty ? "(empty)" : mount.Weapon.Name;
        int nw = Raylib.MeasureText(name, 17);
        Raylib.DrawText(name, centreX - nw / 2, (int)panel.Y + 356, 17,
            mount.IsEmpty ? Muted : WeaponColorOf(mount));

        const string hint = "scroll or arrows to change side";
        int hw = Raylib.MeasureText(hint, 13);
        Raylib.DrawText(hint, centreX - hw / 2, (int)panel.Y + 384, 13, Muted);
    }

    /// <summary>
    /// Turret drawn with each side in its weapon's colour and the selected side
    /// thickened, so the preview alone communicates the whole loadout.
    /// </summary>
    private void DrawTurretPreview(Player player, Vector2 centre)
    {
        int n = player.SideCount;
        const float radius = 88f;

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
            Color color = mount.IsEmpty ? Disabled : WeaponColorOf(mount);

            Raylib.DrawLineEx(a, b, i == SelectedSide ? 7f : 3f, color);

            // Number each side against its edge so the preview maps to the count.
            Vector2 mid = (a + b) * 0.5f;
            Vector2 outward = Vector2.Normalize(mid - centre);
            Vector2 label = mid + outward * 16f;

            string text = (i + 1).ToString();
            int tw = Raylib.MeasureText(text, 16);
            Raylib.DrawText(text, (int)label.X - tw / 2, (int)label.Y - 8, 16,
                i == SelectedSide ? Ink : Muted);
        }
    }

    private static Color WeaponColorOf(Mount mount) => FromPacked(mount.Weapon.Def.PackedTint);

    private static Color FromPacked(uint t) => new(
        (int)((t >> 24) & 0xFF),
        (int)((t >> 16) & 0xFF),
        (int)((t >> 8) & 0xFF),
        255);

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

        if (mount.IsEmpty)
        {
            DrawWeaponPicker(world, shop, x, y + 44, panel);
            return;
        }

        // Rows come from the catalogue filtered by the fitted weapon, so a
        // weapon-specific upgrade appears purely from its appliesTo entry.
        world.UpgradeDefs.CollectMountUpgrades(mount.Weapon.Def.Id, _rows);

        int rowY = y + 44;
        foreach (UpgradeDef def in _rows)
        {
            DrawUpgradeRow(world, shop, def, x, rowY, panel);
            rowY += RowHeight;
        }

        DrawCurrentStats(mount, x, rowY + 4);
        DrawWeaponPicker(world, shop, x, rowY + 32, panel);
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
            Raylib.DrawRectangleRec(row, HoverFill);
            Raylib.DrawRectangleLinesEx(row, 1.5f, HoverEdge);
        }

        if (buyable && MenuUi.Clicked(row)) shop.Buy(def, SelectedSide);

        Raylib.DrawText(def.Name, x, y, 19, maxed ? Maxed : Ink);

        string levelText = maxed ? $"Lv {level}  MAX" : $"Lv {level} -> {level + 1}";
        Raylib.DrawText(levelText, x + 170, y, 18, maxed ? Maxed : Muted);

        string cost = maxed ? "MAX" : $"{def.CostFor(level)}c";
        int cw = Raylib.MeasureText(cost, 19);
        Raylib.DrawText(cost, right - cw, y, 19,
            maxed ? Maxed : affordable ? Gold : Disabled);

        DrawLevelBar(def, level, maxed, x, y + 26, right - x);
    }

    private static void DrawLevelBar(UpgradeDef def, int level, bool maxed, int x, int y, int width)
    {
        int segments = def.MaxLevel > 0 ? def.MaxLevel : 10;
        float segWidth = width / (float)segments;

        Color filled = maxed ? Maxed : Gold;

        for (int i = 0; i < segments; i++)
        {
            var seg = new Rectangle(x + i * segWidth, y, segWidth - 3f, 8f);
            Raylib.DrawRectangleRec(seg, i < level ? filled : new Color(224, 224, 230, 255));
        }
    }

    /// <summary>
    /// Resolved values for the fitted weapon, so the effect of a purchase is
    /// visible immediately rather than only in play.
    /// </summary>
    private static void DrawCurrentStats(Mount mount, int x, int y)
    {
        if (mount.IsEmpty) return;

        StatBlock stats = mount.Weapon.Stats;

        string text =
            $"dmg {stats.Get(StatId.Damage):0.#}   " +
            $"rate {stats.Get(StatId.FireRate):0.##}/s   " +
            $"shots {stats.GetInt(StatId.ProjectileCount)}";

        if (mount.Weapon.Def.OnHit == OnHit.Explode)
            text += $"   blast {stats.Get(StatId.ExplosionRadius):0}";

        Raylib.DrawText(text, x, y, 16, Muted);
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
        int rowY = y + 22;

        foreach (WeaponDef def in world.Weapons.Weapons)
        {
            bool isFitted = !mount.IsEmpty && mount.Weapon.Def.Id == def.Id;
            bool affordable = world.Coins >= cost;
            bool clickable = !isFitted && affordable;

            var row = new Rectangle(x - 8, rowY - 4, right - x + 16, PickerRowHeight - 2);

            if (MenuUi.IsHovered(row) && clickable)
            {
                Raylib.DrawRectangleRec(row, HoverFill);
                Raylib.DrawRectangleLinesEx(row, 1.5f, HoverEdge);
            }

            if (clickable && MenuUi.Clicked(row)) shop.BuyEquip(equip, SelectedSide, def);

            Raylib.DrawRectangleRec(new Rectangle(x, rowY + 2, 12f, 12f), FromPacked(def.PackedTint));

            Raylib.DrawText(def.Name, x + 22, rowY, 17,
                isFitted ? Maxed : clickable ? Ink : Disabled);

            string trailing = isFitted ? "fitted" : $"{cost}c";
            int tw = Raylib.MeasureText(trailing, 15);
            Raylib.DrawText(trailing, right - tw, rowY + 1, 15,
                isFitted ? Maxed : affordable ? Gold : Disabled);

            rowY += PickerRowHeight;
        }

        if (!mount.IsEmpty)
            Raylib.DrawText("Replacing resets this side's upgrade levels.", x, rowY + 2, 14, Muted);
    }

    // --- Footer: run-wide purchases --------------------------------------

    private void DrawFooter(World world, Rectangle panel, Shop shop)
    {
        int y = (int)(panel.Y + panel.Height) - 80;
        float width = (panel.Width - 60) / 3f;

        DrawFooterButton(world, shop, world.UpgradeDefs.Find("shape"),
            new Rectangle(panel.X + 24, y, width - 10, 42),
            ShapeLabel(world.Player));

        DrawFooterButton(world, shop, world.UpgradeDefs.Find("maxhealth"),
            new Rectangle(panel.X + 24 + width, y, width - 10, 42),
            MaxHealthLabel(world.Player));

        DrawFooterButton(world, shop, world.UpgradeDefs.Find("repair"),
            new Rectangle(panel.X + 24 + width * 2, y, width - 10, 42),
            RepairLabel(world.Player));
    }

    /// <summary>Names the shape being bought, so the cost has something concrete attached.</summary>
    private static string ShapeLabel(Player player)
    {
        if (player.SideCount >= Player.MaxSides) return "Max sides";

        string next = player.SideCount switch
        {
            3 => "Square",
            4 => "Pentagon",
            5 => "Hexagon",
            6 => "Heptagon",
            7 => "Octagon",
            _ => $"{player.SideCount + 1} sides",
        };

        return $"Add Side ({next})";
    }

    private static string MaxHealthLabel(Player player) => $"Max Health ({player.MaxHealth:0})";

    private static string RepairLabel(Player player) =>
        player.Health >= player.MaxHealth ? "Repair (at full)" : "Repair to Full";

    private void DrawFooterButton(World world, Shop shop, UpgradeDef def, Rectangle rect, string labelOverride)
    {
        if (def == null) return;

        bool buyable = shop.CanBuy(def, SelectedSide);
        bool maxed = shop.IsMaxed(def, SelectedSide);
        bool hovered = MenuUi.IsHovered(rect);

        Raylib.DrawRectangleRec(rect, buyable && hovered ? HoverFill : new Color(238, 238, 244, 255));
        Raylib.DrawRectangleLinesEx(rect, 1.5f,
            maxed ? Maxed : buyable ? HoverEdge : new Color(214, 214, 222, 255));

        if (buyable && MenuUi.Clicked(rect)) shop.Buy(def, SelectedSide);

        string label = labelOverride ?? def.Name;
        Raylib.DrawText(label, (int)rect.X + 12, (int)rect.Y + 7, 17,
            maxed ? Maxed : buyable ? Ink : Muted);

        int level = shop.LevelOf(def, SelectedSide);
        string cost = maxed ? "MAX" : $"{def.CostFor(level)}c";
        int cw = Raylib.MeasureText(cost, 16);

        Raylib.DrawText(cost, (int)(rect.X + rect.Width) - 12 - cw, (int)rect.Y + 22, 16,
            maxed ? Maxed : buyable ? Gold : Disabled);
    }

    private static void DrawFooterHint(Rectangle panel)
    {
        const string hint = "Right-click to close";
        int w = Raylib.MeasureText(hint, 15);
        Raylib.DrawText(hint,
            (int)(panel.X + panel.Width / 2) - w / 2,
            (int)(panel.Y + panel.Height) - 28, 15, Muted);
    }
}
