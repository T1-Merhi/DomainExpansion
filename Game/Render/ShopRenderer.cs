/// <summary>
/// The upgrade overlay.
///
/// Interaction and drawing are separate passes: HandleInput runs from the
/// scene's Update and is the only thing that mutates anything; Draw only
/// draws. Layout comes from pure functions of the screen size used by both, so
/// the two can never disagree about where a control is.
/// </summary>
public sealed class ShopRenderer
{
    private const int PanelWidth = 880;
    private const int PanelHeight = 560;
    private const int LeftColumnWidth = 320;
    private const int RowHeight = 56;
    private const int PickerRowHeight = 28;
    private const int DetailTop = 96;

    private static readonly Color Panel = new(246, 246, 249, 250);
    private static readonly Color PanelEdge = new(70, 70, 82, 255);
    private static readonly Color Ink = new(45, 45, 55, 255);
    private static readonly Color Muted = new(130, 130, 142, 255);
    private static readonly Color Gold = new(214, 168, 40, 255);
    private static readonly Color Maxed = new(56, 158, 74, 255);
    private static readonly Color Disabled = new(190, 190, 198, 255);
    private static readonly Color HoverFill = new(236, 240, 232, 255);
    private static readonly Color HoverEdge = new(120, 170, 110, 255);

    private static readonly string[] PlacedPlayerUpgrades = ["maxhealth"];

    /// <summary>Which side's detail is shown. Owned here; the sim is untouched.</summary>
    public int SelectedSide;

    private float _detailScroll;

    /// <summary>Reused so building the upgrade row list does not allocate per frame.</summary>
    private readonly List<UpgradeDef> _rows = new();

    // --- Layout -----------------------------------------------------------

    private static Rectangle PanelRect() => new(
        (Raylib.GetScreenWidth() - PanelWidth) / 2f,
        (Raylib.GetScreenHeight() - PanelHeight) / 2f,
        PanelWidth, PanelHeight);

    private static Rectangle DetailRect(Rectangle panel) => new(
        panel.X + LeftColumnWidth + 20,
        panel.Y + DetailTop,
        panel.Width - LeftColumnWidth - 50,
        panel.Height - DetailTop - 110);

    private static int DetailRight(Rectangle panel) => (int)(panel.X + panel.Width) - 30;

    private Rectangle UpgradeRowRect(Rectangle panel, int index)
    {
        Rectangle detail = DetailRect(panel);
        float y = detail.Y + 44 + index * RowHeight - _detailScroll;

        return new Rectangle(detail.X - 8, y - 6, DetailRight(panel) - detail.X + 16, RowHeight - 8);
    }

    private static Rectangle FooterRect(Rectangle panel, int slot, int row)
    {
        float width = (panel.Width - 60) / 3f;
        float y = panel.Y + panel.Height - 80 - row * 48;

        return new Rectangle(panel.X + 24 + width * slot, y, width - 10, 42);
    }

    /// <summary>Y of the first weapon-picker row, which sits below the upgrade rows.</summary>
    private float PickerTop(Rectangle panel, Mount mount)
    {
        Rectangle detail = DetailRect(panel);

        if (mount.IsEmpty) return detail.Y + 50;

        return detail.Y + 44 + _rows.Count * RowHeight + 48 - _detailScroll;
    }

    // --- Interaction ------------------------------------------------------

    public void HandleInput(World world, Shop shop)
    {
        Rectangle panel = PanelRect();
        Player player = world.Player;

        ClampSelection(player);
        HandleScroll(panel, player);

        Mount mount = player.Mounts[SelectedSide];

        if (!mount.IsEmpty)
        {
            world.UpgradeDefs.CollectMountUpgrades(mount.Weapon.Def.Id, _rows);

            for (int i = 0; i < _rows.Count; i++)
            {
                Rectangle row = UpgradeRowRect(panel, i);

                if (!MenuUi.Clicked(row)) continue;
                if (shop.CanBuy(_rows[i], SelectedSide)) shop.Buy(_rows[i], SelectedSide);
            }
        }

        HandlePickerInput(world, shop, panel, mount);
        HandleFooterInput(world, shop, panel);
    }

    private void ClampSelection(Player player)
    {
        if (SelectedSide >= player.SideCount || SelectedSide < 0) SelectedSide = 0;
    }

    /// <summary>
    /// The wheel changes side over the turret and scrolls over the detail
    /// column. One wheel serving both is unambiguous only because the two live
    /// in different halves of the panel.
    /// </summary>
    private void HandleScroll(Rectangle panel, Player player)
    {
        int wheel = (int)Raylib.GetMouseWheelMove();
        bool overDetail = MenuUi.IsHovered(DetailRect(panel));

        if (overDetail && wheel != 0)
        {
            _detailScroll = MathF.Max(0f, _detailScroll - wheel * 40f);
            return;
        }

        int delta = overDetail ? 0 : wheel;

        if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.Down)) delta += 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.Up)) delta -= 1;

        if (delta == 0) return;

        int n = player.SideCount;
        SelectedSide = ((SelectedSide + delta) % n + n) % n;

        // A different side has a different row count, so a carried-over scroll
        // could leave the panel showing empty space.
        _detailScroll = 0f;
    }

    private void HandlePickerInput(World world, Shop shop, Rectangle panel, Mount mount)
    {
        UpgradeDef equip = world.UpgradeDefs.Find("equip");
        if (equip == null) return;

        Rectangle detail = DetailRect(panel);
        int cost = shop.CostOf(equip, SelectedSide);

        float y = PickerTop(panel, mount);

        foreach (WeaponDef def in world.Weapons.Weapons)
        {
            bool isFitted = !mount.IsEmpty && mount.Weapon.Def.Id == def.Id;
            bool clickable = !isFitted && world.Coins >= cost;

            var row = new Rectangle(detail.X - 8, y - 4, DetailRight(panel) - detail.X + 16, PickerRowHeight - 2);

            if (clickable && MenuUi.Clicked(row)) shop.BuyEquip(equip, SelectedSide, def);

            y += PickerRowHeight;
        }
    }

    private void HandleFooterInput(World world, Shop shop, Rectangle panel)
    {
        TryFooterClick(shop, world.UpgradeDefs.Find("shape"), FooterRect(panel, 0, 0));
        TryFooterClick(shop, world.UpgradeDefs.Find("maxhealth"), FooterRect(panel, 1, 0));
        TryFooterClick(shop, world.UpgradeDefs.Find("repair"), FooterRect(panel, 2, 0));

        int slot = 0;
        foreach (UpgradeDef def in world.UpgradeDefs.Upgrades)
        {
            if (def.Kind != UpgradeKind.PlayerStat) continue;
            if (Array.IndexOf(PlacedPlayerUpgrades, def.Id) >= 0) continue;

            TryFooterClick(shop, def, FooterRect(panel, slot, 1));
            if (++slot >= 3) return;
        }
    }

    private void TryFooterClick(Shop shop, UpgradeDef def, Rectangle rect)
    {
        if (def == null || !MenuUi.Clicked(rect)) return;
        if (shop.CanBuy(def, SelectedSide)) shop.Buy(def, SelectedSide);
    }

    // --- Drawing ----------------------------------------------------------

    public void Draw(World world, Shop shop)
    {
        Rectangle panel = PanelRect();

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color(15, 15, 25, 140));

        Raylib.DrawRectangleRec(panel, Panel);
        Raylib.DrawRectangleLinesEx(panel, 2.5f, PanelEdge);

        DrawHeader(world, panel);
        DrawSidePanel(world, panel);

        // Clipped, so a long upgrade list scrolls inside the column instead of
        // spilling over the footer buttons.
        Rectangle detail = DetailRect(panel);
        Raylib.BeginScissorMode((int)detail.X - 12, (int)detail.Y - 4,
            (int)detail.Width + 24, (int)detail.Height + 8);

        DrawDetail(world, panel, shop);

        Raylib.EndScissorMode();

        DrawFooter(world, panel, shop);
        DrawFooterHint(panel);
    }

    private void DrawHeader(World world, Rectangle panel)
    {
        Raylib.DrawText("UPGRADES", (int)panel.X + 24, (int)panel.Y + 20, 30, Ink);

        string coins = $"{world.Coins}";
        int w = Raylib.MeasureText(coins, 30);
        Raylib.DrawText(coins, (int)(panel.X + panel.Width) - 24 - w, (int)panel.Y + 20, 30, Gold);

        const string label = "COINS";
        int lw = Raylib.MeasureText(label, 13);
        Raylib.DrawText(label, (int)(panel.X + panel.Width) - 24 - lw, (int)panel.Y + 52, 13, Muted);

        Raylib.DrawRectangleRec(new Rectangle(panel.X + 20, panel.Y + 74, panel.Width - 40, 1.5f),
            new Color(215, 215, 222, 255));
    }

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

    private void DrawTurretPreview(Player player, Vector2 centre)
    {
        int n = player.SideCount;
        const float radius = 88f;

        Span<Vector2> verts = stackalloc Vector2[Player.MaxSidesCeiling];

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

            Vector2 mid = (a + b) * 0.5f;
            Vector2 label = mid + Vector2.Normalize(mid - centre) * 16f;

            string text = (i + 1).ToString();
            int tw = Raylib.MeasureText(text, 16);
            Raylib.DrawText(text, (int)label.X - tw / 2, (int)label.Y - 8, 16,
                i == SelectedSide ? Ink : Muted);
        }
    }

    private static Color WeaponColorOf(Mount mount) => FromPacked(mount.Weapon.Def.PackedTint);

    private static Color FromPacked(uint t) => new(
        (int)((t >> 24) & 0xFF), (int)((t >> 16) & 0xFF), (int)((t >> 8) & 0xFF), 255);

    private void DrawDetail(World world, Rectangle panel, Shop shop)
    {
        Player player = world.Player;
        Mount mount = player.Mounts[SelectedSide];

        Rectangle detail = DetailRect(panel);
        int x = (int)detail.X;

        string title = mount.IsEmpty
            ? $"SIDE {SelectedSide + 1} - EMPTY"
            : $"SIDE {SelectedSide + 1} - {mount.Weapon.Name.ToUpperInvariant()}";

        Raylib.DrawText(title, x, (int)detail.Y, 22, Ink);

        if (!mount.IsEmpty)
        {
            world.UpgradeDefs.CollectMountUpgrades(mount.Weapon.Def.Id, _rows);

            for (int i = 0; i < _rows.Count; i++)
                DrawUpgradeRow(shop, _rows[i], panel, UpgradeRowRect(panel, i));

            float statsY = detail.Y + 44 + _rows.Count * RowHeight + 6 - _detailScroll;
            DrawCurrentStats(mount, x, (int)statsY);
        }

        DrawWeaponPicker(world, shop, panel, mount);
    }

    private void DrawUpgradeRow(Shop shop, UpgradeDef def, Rectangle panel, Rectangle row)
    {
        if (def == null) return;

        int right = DetailRight(panel);
        int x = (int)row.X + 8;
        int y = (int)row.Y + 6;

        int level = shop.LevelOf(def, SelectedSide);
        bool maxed = shop.IsMaxed(def, SelectedSide);
        bool affordable = shop.CanAfford(def, SelectedSide);
        bool buyable = shop.CanBuy(def, SelectedSide);

        if (MenuUi.IsHovered(row) && buyable)
        {
            Raylib.DrawRectangleRec(row, HoverFill);
            Raylib.DrawRectangleLinesEx(row, 1.5f, HoverEdge);
        }

        Raylib.DrawText(def.Name, x, y, 19, maxed ? Maxed : Ink);

        string levelText = maxed ? $"Lv {level}  MAX" : $"Lv {level} -> {level + 1}";
        Raylib.DrawText(levelText, x + 170, y, 18, maxed ? Maxed : Muted);

        string cost = maxed ? "MAX" : $"{def.CostFor(level)}c";
        int cw = Raylib.MeasureText(cost, 19);
        Raylib.DrawText(cost, right - cw, y, 19, maxed ? Maxed : affordable ? Gold : Disabled);

        DrawLevelBar(def, level, maxed, x, y + 26, right - x);
    }

    private static void DrawLevelBar(UpgradeDef def, int level, bool maxed, int x, int y, int width)
    {
        int segments = def.MaxLevel > 0 ? def.MaxLevel : 10;
        float segWidth = width / (float)segments;

        Color filled = maxed ? Maxed : Gold;

        for (int i = 0; i < segments; i++)
        {
            Raylib.DrawRectangleRec(new Rectangle(x + i * segWidth, y, segWidth - 3f, 8f),
                i < level ? filled : new Color(224, 224, 230, 255));
        }
    }

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

    private void DrawWeaponPicker(World world, Shop shop, Rectangle panel, Mount mount)
    {
        UpgradeDef equip = world.UpgradeDefs.Find("equip");
        if (equip == null) return;

        Rectangle detail = DetailRect(panel);
        int x = (int)detail.X;
        int right = DetailRight(panel);

        float y = PickerTop(panel, mount);

        string heading = mount.IsEmpty ? "FIT A WEAPON" : "REPLACE WEAPON";
        Raylib.DrawText(heading, x, (int)y - 22, 15, Muted);

        int cost = shop.CostOf(equip, SelectedSide);

        foreach (WeaponDef def in world.Weapons.Weapons)
        {
            bool isFitted = !mount.IsEmpty && mount.Weapon.Def.Id == def.Id;
            bool affordable = world.Coins >= cost;
            bool clickable = !isFitted && affordable;

            var row = new Rectangle(x - 8, y - 4, right - x + 16, PickerRowHeight - 2);

            if (MenuUi.IsHovered(row) && clickable)
            {
                Raylib.DrawRectangleRec(row, HoverFill);
                Raylib.DrawRectangleLinesEx(row, 1.5f, HoverEdge);
            }

            Raylib.DrawRectangleRec(new Rectangle(x, y + 2, 12f, 12f), FromPacked(def.PackedTint));

            Raylib.DrawText(def.Name, x + 22, (int)y, 17,
                isFitted ? Maxed : clickable ? Ink : Disabled);

            string trailing = isFitted ? "fitted" : $"{cost}c";
            int tw = Raylib.MeasureText(trailing, 15);
            Raylib.DrawText(trailing, right - tw, (int)y + 1, 15,
                isFitted ? Maxed : affordable ? Gold : Disabled);

            y += PickerRowHeight;
        }

        if (!mount.IsEmpty)
            Raylib.DrawText("Replacing resets this side's upgrade levels.", x, (int)y + 2, 14, Muted);
    }

    private void DrawFooter(World world, Rectangle panel, Shop shop)
    {
        DrawFooterButton(world, shop, world.UpgradeDefs.Find("shape"),
            FooterRect(panel, 0, 0), ShapeLabel(world.Player));

        DrawFooterButton(world, shop, world.UpgradeDefs.Find("maxhealth"),
            FooterRect(panel, 1, 0), MaxHealthLabel(world.Player));

        DrawFooterButton(world, shop, world.UpgradeDefs.Find("repair"),
            FooterRect(panel, 2, 0), RepairLabel(world.Player));

        // Anything of kind PlayerStat not placed above gets a second row, so an
        // upgrade added to JSON cannot end up defined but unreachable.
        int slot = 0;
        foreach (UpgradeDef def in world.UpgradeDefs.Upgrades)
        {
            if (def.Kind != UpgradeKind.PlayerStat) continue;
            if (Array.IndexOf(PlacedPlayerUpgrades, def.Id) >= 0) continue;

            DrawFooterButton(world, shop, def, FooterRect(panel, slot, 1),
                $"{def.Name} (Lv {shop.LevelOf(def, SelectedSide)})");

            if (++slot >= 3) return;
        }
    }

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

        Raylib.DrawText(labelOverride ?? def.Name, (int)rect.X + 12, (int)rect.Y + 7, 17,
            maxed ? Maxed : buyable ? Ink : Muted);

        int level = shop.LevelOf(def, SelectedSide);
        string cost = maxed ? "MAX" : $"{def.CostFor(level)}c";
        int cw = Raylib.MeasureText(cost, 16);

        Raylib.DrawText(cost, (int)(rect.X + rect.Width) - 12 - cw, (int)rect.Y + 22, 16,
            maxed ? Maxed : buyable ? Gold : Disabled);
    }

    private static void DrawFooterHint(Rectangle panel)
    {
        const string hint = "Right-click to close   -   the arena keeps moving";
        int w = Raylib.MeasureText(hint, 15);
        Raylib.DrawText(hint, (int)(panel.X + panel.Width / 2) - w / 2,
            (int)(panel.Y + panel.Height) - 28, 15, Muted);
    }
}
