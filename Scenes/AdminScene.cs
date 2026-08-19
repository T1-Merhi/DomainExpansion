/// <summary>
/// The tuning editor. Admin mode only - SceneManager never routes here in
/// player mode, and the main menu does not offer it.
/// </summary>
public class AdminScene : IScene
{
    public event Action<GameEvent> EventRaised;

    private static readonly string[] Files =
    [
        "player.json", "weapons.json", "enemies.json",
        "waves.json", "upgrades.json", "effects.json",
    ];

    private const int TabHeight = 38;
    private const int RowHeight = 30;
    private const int LabelWidth = 300;

    private static readonly Color Ink = new(45, 45, 55, 255);
    private static readonly Color Muted = new(130, 130, 142, 255);
    private static readonly Color Dirty = new(206, 122, 20, 255);
    private static readonly Color Accent = new(56, 118, 200, 255);
    private static readonly Color GroupInk = new(80, 80, 95, 255);

    private AssetManager _assets;
    private GameSettings _settings;

    private readonly List<ConfigDocument> _docs = new();
    private int _activeTab;
    private float _scroll;
    private string _status = "";
    private double _statusUntil;

    private int _draggingField = -1;
    private bool _confirmingExit;

    public void Init(AssetManager assets, GameSettings settings)
    {
        _assets = assets;
        _settings = settings;

        _docs.Clear();
        foreach (string file in Files) _docs.Add(new ConfigDocument(file));

        _activeTab = 0;
        _scroll = 0f;
    }

    private ConfigDocument Active => _docs[Math.Clamp(_activeTab, 0, _docs.Count - 1)];

    public void Update(float deltaTime)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) TryLeave();

        if (Raylib.IsKeyPressed(KeyboardKey.Tab)) SwitchTab((_activeTab + 1) % _docs.Count);

        // Ctrl+S is the reflex, so support it alongside the button.
        if (Raylib.IsKeyDown(KeyboardKey.LeftControl) && Raylib.IsKeyPressed(KeyboardKey.S))
            SaveActive();

        if (_draggingField < 0) _scroll -= Raylib.GetMouseWheelMove() * 48f;
        _scroll = MathF.Max(0f, _scroll);
    }

    /// <summary>
    /// Tab switching keeps unsaved edits: the document holds them in memory, so
    /// nothing is lost by looking at another file. The drag must be released
    /// though - keeping the index would apply the next drag to whichever field
    /// happens to sit at that position in the new tab.
    /// </summary>
    private void SwitchTab(int index)
    {
        _activeTab = index;
        _scroll = 0f;
        _draggingField = -1;
    }

    /// <summary>
    /// Leaving with unsaved edits needs confirming. This scene exists to make
    /// those edits, so discarding a session's worth on one keypress is the
    /// worst thing it could do silently.
    /// </summary>
    private void TryLeave()
    {
        if (!AnyDirty() || _confirmingExit)
        {
            EventRaised?.Invoke(GameEvent.MainMenuRequested);
            return;
        }

        _confirmingExit = true;
        Flash("Unsaved changes - Ctrl+S to save, ESC again to discard");
    }

    private bool AnyDirty()
    {
        foreach (ConfigDocument doc in _docs) if (doc.IsDirty) return true;
        return false;
    }

    private void SaveActive()
    {
        bool saved = Active.Save();
        if (saved) _confirmingExit = false;

        Flash(saved ? $"Saved {Active.FileName}" : $"Could not save {Active.FileName}");
    }

    private void Flash(string message)
    {
        _status = message;
        _statusUntil = Raylib.GetTime() + 3.0;
    }

    public void Draw()
    {
        Raylib.DrawText("ADMIN - CONFIG EDITOR", 24, 20, 28, Ink);

        DrawConfigLocation();
        DrawTabs();
        DrawToolbar();

        Rectangle view = ViewRect();
        Raylib.BeginScissorMode((int)view.X, (int)view.Y, (int)view.Width, (int)view.Height);
        DrawFields(view);
        Raylib.EndScissorMode();

        DrawStatus();
    }

    private void DrawConfigLocation()
    {
        string path = ConfigPaths.SharedDir;
        int w = Raylib.MeasureText(path, 13);
        Raylib.DrawText(path, Raylib.GetScreenWidth() - 24 - w, 26, 13, Muted);

        ConfigStore store = ConfigStore.Current;
        string version = $"config v{store.Version}  loaded {store.LoadedAt:HH:mm:ss}";
        int vw = Raylib.MeasureText(version, 13);
        Raylib.DrawText(version, Raylib.GetScreenWidth() - 24 - vw, 44, 13,
            store.HasWarning ? Dirty : Muted);
    }

    private void DrawTabs()
    {
        int x = 24;
        const int y = 62;

        for (int i = 0; i < _docs.Count; i++)
        {
            ConfigDocument doc = _docs[i];

            string label = doc.Title + (doc.IsDirty ? " *" : "");
            int w = Raylib.MeasureText(label, 17) + 26;

            var tab = new Rectangle(x, y, w, TabHeight);
            bool active = i == _activeTab;

            Raylib.DrawRectangleRec(tab, active ? new Color(232, 238, 248, 255) : new Color(240, 240, 245, 255));
            Raylib.DrawRectangleLinesEx(tab, 1.5f, active ? Accent : new Color(216, 216, 224, 255));

            if (MenuUi.Clicked(tab)) SwitchTab(i);

            Raylib.DrawText(label, x + 13, y + 10, 17,
                doc.IsDirty ? Dirty : active ? Accent : Muted);

            x += w + 6;
        }
    }

    private void DrawToolbar()
    {
        const int y = 108;
        int x = 24;

        x = DrawButton("Save  (Ctrl+S)", x, y, () => SaveActive());
        x = DrawButton("Revert All", x, y, () => { Active.RevertAll(); Flash("Reverted unsaved edits"); });
        x = DrawButton("Reset to Default", x, y, () =>
        {
            Flash(Active.ResetToDefault()
                ? $"Reset {Active.FileName} to shipped default"
                : "No shipped default found");
        });

        DrawButton("Apply to Game", x, y, () =>
        {
            ConfigStore.Current.Reload();
            Flash($"Reloaded config v{ConfigStore.Current.Version}");
        });
    }

    private int DrawButton(string label, int x, int y, Action onClick)
    {
        int w = Raylib.MeasureText(label, 16) + 26;
        var rect = new Rectangle(x, y, w, 30);

        bool hovered = MenuUi.IsHovered(rect);

        Raylib.DrawRectangleRec(rect, hovered ? new Color(232, 238, 248, 255) : new Color(240, 240, 245, 255));
        Raylib.DrawRectangleLinesEx(rect, 1.5f, hovered ? Accent : new Color(212, 212, 220, 255));
        Raylib.DrawText(label, x + 13, y + 7, 16, Ink);

        if (MenuUi.Clicked(rect)) onClick();

        return x + w + 8;
    }

    private static Rectangle ViewRect() => new(
        24, 150,
        Raylib.GetScreenWidth() - 48,
        Raylib.GetScreenHeight() - 150 - 46);

    private void DrawFields(Rectangle view)
    {
        ConfigDocument doc = Active;

        if (doc.LoadFailed)
        {
            Raylib.DrawText($"Could not read {doc.FileName}", (int)view.X, (int)view.Y, 18, Dirty);
            return;
        }

        float y = view.Y - _scroll;
        string lastGroup = null;

        for (int i = 0; i < doc.Fields.Count; i++)
        {
            ConfigField field = doc.Fields[i];

            if (field.Group != lastGroup)
            {
                lastGroup = field.Group;

                if (!string.IsNullOrEmpty(lastGroup))
                {
                    y += 10;
                    if (Visible(view, y)) Raylib.DrawText(lastGroup.ToUpperInvariant(), (int)view.X, (int)y, 15, GroupInk);
                    y += 24;
                }
            }

            if (Visible(view, y)) DrawField(field, i, view, y);
            y += RowHeight;
        }

        // Release a drag anywhere, so letting go outside the slider still ends it.
        if (!Raylib.IsMouseButtonDown(MouseButton.Left)) _draggingField = -1;
    }

    private static bool Visible(Rectangle view, float y) => y > view.Y - RowHeight && y < view.Y + view.Height;

    private void DrawField(ConfigField field, int index, Rectangle view, float y)
    {
        int x = (int)view.X + field.Depth * 10;

        Raylib.DrawText(field.Label, x, (int)y + 4, 16, field.IsDirty ? Dirty : Ink);

        if (field.IsDirty) Raylib.DrawText("*", x - 12, (int)y + 4, 16, Dirty);

        int controlX = (int)view.X + LabelWidth;
        int controlWidth = (int)(view.Width - LabelWidth - 110);

        switch (field.Kind)
        {
            case FieldKind.Number:
                DrawNumberField(field, index, controlX, (int)y, controlWidth);
                break;

            case FieldKind.Bool:
                DrawBoolField(field, controlX, (int)y);
                break;

            default:
                Raylib.DrawText(field.CurrentText, controlX, (int)y + 4, 16, Muted);
                break;
        }
    }

    private void DrawNumberField(ConfigField field, int index, int x, int y, int width)
    {
        var track = new Rectangle(x, y + 11, width, 6);
        Raylib.DrawRectangleRounded(track, 1f, 4, new Color(216, 216, 224, 255));

        float span = MathF.Max(0.0001f, field.Max - field.Min);
        float t = Math.Clamp((field.CurrentNumber - field.Min) / span, 0f, 1f);

        Raylib.DrawRectangleRounded(new Rectangle(x, y + 11, width * t, 6), 1f, 4, Accent);

        var hit = new Rectangle(x, y, width, RowHeight - 4);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && MenuUi.IsHovered(hit)) _draggingField = index;

        if (_draggingField == index && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            float ratio = (Raylib.GetMousePosition().X - x) / width;
            field.SetNumber(field.Min + Math.Clamp(ratio, 0f, 1f) * span);
        }

        Raylib.DrawCircleV(new Vector2(x + width * t, y + 14), 7f, Accent);

        string value = field.CurrentNumber.ToString(field.Step >= 1f ? "0" : "0.###");
        Raylib.DrawText(value, x + width + 14, y + 4, 16, field.IsDirty ? Dirty : Ink);
    }

    private void DrawBoolField(ConfigField field, int x, int y)
    {
        var box = new Rectangle(x, y + 4, 22, 22);

        Raylib.DrawRectangleRec(box, Color.RayWhite);
        Raylib.DrawRectangleLinesEx(box, 2f, field.IsDirty ? Dirty : Muted);

        if (field.CurrentBool)
            Raylib.DrawRectangleRec(new Rectangle(box.X + 5, box.Y + 5, 12, 12), Accent);

        if (MenuUi.Clicked(box)) field.SetBool(!field.CurrentBool);
    }

    private void DrawStatus()
    {
        int y = Raylib.GetScreenHeight() - 34;

        Raylib.DrawText("TAB switch file   scroll to move   ESC back to menu", 24, y, 15, Muted);

        if (Raylib.GetTime() > _statusUntil || string.IsNullOrEmpty(_status)) return;

        int w = Raylib.MeasureText(_status, 16);
        Raylib.DrawText(_status, Raylib.GetScreenWidth() - 24 - w, y - 1, 16, Accent);
    }

    public void Unload()
    {
    }
}
