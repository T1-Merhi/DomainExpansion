/// <summary>
/// Immediate-mode menu widgets drawn with raylib primitives.
///
/// Each widget draws itself and reports interaction; it holds no state of its
/// own, so the calling scene stays the single source of truth for selection
/// and values. Slider drag state is the one exception and is passed in and out
/// explicitly by the caller.
/// </summary>
public static class MenuUi
{
    public static readonly Color Text = new(60, 60, 70, 255);
    public static readonly Color TextDim = new(130, 130, 140, 255);
    public static readonly Color Accent = new(200, 80, 40, 255);
    public static readonly Color Track = new(205, 205, 212, 255);
    public static readonly Color Fill = new(200, 80, 40, 255);

    public static void CentredText(string text, int centreX, int y, int size, Color color)
    {
        int w = Raylib.MeasureText(text, size);
        Raylib.DrawText(text, centreX - w / 2, y, size, color);
    }

    /// <summary>
    /// True only when the pointer moved this frame. Hover that asserts itself
    /// every frame fights the keyboard: the arrows move the selection and a
    /// stationary cursor immediately drags it back.
    /// </summary>
    public static bool MouseMoved()
    {
        Vector2 delta = Raylib.GetMouseDelta();
        return delta.LengthSquared() > 0.01f;
    }

    public static bool HoverSelects(Rectangle rect) => MouseMoved() && IsHovered(rect);

    public static bool IsHovered(Rectangle rect) =>
        Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);

    public static bool Clicked(Rectangle rect) =>
        IsHovered(rect) && Raylib.IsMouseButtonPressed(MouseButton.Left);

    /// <summary>Centred button. Returns true on click.</summary>
    public static bool Button(Rectangle rect, string label, bool selected)
    {
        if (selected)
        {
            Raylib.DrawRectangleRec(rect, new Color(235, 235, 240, 255));
            Raylib.DrawRectangleLinesEx(rect, 2f, Accent);
        }

        int centreX = (int)(rect.X + rect.Width / 2);
        int size = 24;
        int y = (int)(rect.Y + (rect.Height - size) / 2);
        CentredText(label, centreX, y, size, selected ? Accent : Text);

        return Clicked(rect);
    }

    /// <summary>Label on the left of the row, control area returned on the right.</summary>
    public static void RowLabel(Rectangle row, string label, bool selected)
    {
        int size = 20;
        int y = (int)(row.Y + (row.Height - size) / 2);
        Raylib.DrawText(label, (int)row.X, y, size, selected ? Accent : Text);
    }

    /// <summary>
    /// Draggable slider over 0..1. Returns the new value.
    /// <paramref name="dragging"/> is owned by the caller so a drag that starts
    /// on this slider keeps control even when the cursor leaves its bounds.
    /// </summary>
    public static float Slider(Rectangle rect, float value, bool selected, ref bool dragging)
    {
        const float trackHeight = 6f;
        var track = new Rectangle(rect.X, rect.Y + (rect.Height - trackHeight) / 2f, rect.Width, trackHeight);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && IsHovered(rect)) dragging = true;
        if (!Raylib.IsMouseButtonDown(MouseButton.Left)) dragging = false;

        if (dragging)
        {
            float t = (Raylib.GetMousePosition().X - rect.X) / rect.Width;
            value = Math.Clamp(t, 0f, 1f);
        }

        Raylib.DrawRectangleRounded(track, 1f, 4, Track);

        var filled = new Rectangle(track.X, track.Y, track.Width * value, track.Height);
        if (filled.Width > 0f) Raylib.DrawRectangleRounded(filled, 1f, 4, Fill);

        var knob = new Vector2(rect.X + rect.Width * value, rect.Y + rect.Height / 2f);
        Raylib.DrawCircleV(knob, selected || dragging ? 10f : 8f, selected || dragging ? Accent : Text);
        Raylib.DrawCircleV(knob, selected || dragging ? 6f : 5f, Color.RayWhite);

        return value;
    }

    /// <summary>Checkbox. Returns true when it should toggle.</summary>
    public static bool Checkbox(Rectangle rect, bool value, bool selected)
    {
        float side = MathF.Min(rect.Height, 26f);
        var box = new Rectangle(rect.X, rect.Y + (rect.Height - side) / 2f, side, side);

        Raylib.DrawRectangleRec(box, Color.RayWhite);
        Raylib.DrawRectangleLinesEx(box, 2f, selected ? Accent : TextDim);

        if (value)
        {
            var inner = new Rectangle(box.X + 6, box.Y + 6, box.Width - 12, box.Height - 12);
            Raylib.DrawRectangleRec(inner, selected ? Accent : Text);
        }

        return Clicked(box);
    }
}
