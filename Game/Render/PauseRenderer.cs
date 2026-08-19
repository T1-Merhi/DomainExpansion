public enum PauseAction
{
    None,
    Resume,
    Quit,
}

/// <summary>
/// Small pause panel. Immediate-mode like the other overlays: it draws and
/// reports what was chosen, and the scene decides what that means.
/// </summary>
public sealed class PauseRenderer
{
    private const int PanelWidth = 320;
    private const int PanelHeight = 210;
    private const int ButtonHeight = 46;

    private static readonly string[] Options = ["Resume", "Quit to Menu"];

    private static readonly Color Panel = new(246, 246, 249, 252);
    private static readonly Color PanelEdge = new(70, 70, 82, 255);
    private static readonly Color Ink = new(45, 45, 55, 255);
    private static readonly Color Muted = new(130, 130, 142, 255);
    private static readonly Color Accent = new(200, 80, 40, 255);

    private int _selected;

    /// <summary>Reset when the panel opens, so it never reopens on "Quit".</summary>
    public void Reset() => _selected = 0;

    public PauseAction Draw()
    {
        Rectangle panel = PanelRect();

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color(15, 15, 25, 150));

        Raylib.DrawRectangleRec(panel, Panel);
        Raylib.DrawRectangleLinesEx(panel, 2.5f, PanelEdge);

        int centreX = (int)(panel.X + panel.Width / 2);

        string title = "PAUSED";
        int tw = Raylib.MeasureText(title, 28);
        Raylib.DrawText(title, centreX - tw / 2, (int)panel.Y + 22, 28, Ink);

        HandleKeyboard();

        PauseAction action = PauseAction.None;

        for (int i = 0; i < Options.Length; i++)
        {
            Rectangle row = ButtonRect(panel, i);

            if (MenuUi.IsHovered(row)) _selected = i;

            bool selected = _selected == i;

            if (selected)
            {
                Raylib.DrawRectangleRec(row, new Color(234, 234, 241, 255));
                Raylib.DrawRectangleLinesEx(row, 2f, Accent);
            }

            int size = 21;
            int lw = Raylib.MeasureText(Options[i], size);
            Raylib.DrawText(Options[i], centreX - lw / 2, (int)(row.Y + (row.Height - size) / 2), size,
                selected ? Accent : Ink);

            if (MenuUi.Clicked(row)) action = ActionFor(i);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
            action = ActionFor(_selected);

        string hint = "ESC to resume";
        int hw = Raylib.MeasureText(hint, 14);
        Raylib.DrawText(hint, centreX - hw / 2, (int)(panel.Y + panel.Height) - 26, 14, Muted);

        return action;
    }

    private void HandleKeyboard()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Down))
            _selected = (_selected + 1) % Options.Length;
        else if (Raylib.IsKeyPressed(KeyboardKey.Up))
            _selected = (_selected - 1 + Options.Length) % Options.Length;
    }

    private static PauseAction ActionFor(int index) =>
        index == 0 ? PauseAction.Resume : PauseAction.Quit;

    private static Rectangle PanelRect() => new(
        (Raylib.GetScreenWidth() - PanelWidth) / 2f,
        (Raylib.GetScreenHeight() - PanelHeight) / 2f,
        PanelWidth, PanelHeight);

    private static Rectangle ButtonRect(Rectangle panel, int index) => new(
        panel.X + 30,
        panel.Y + 78 + index * (ButtonHeight + 10),
        panel.Width - 60,
        ButtonHeight);
}
