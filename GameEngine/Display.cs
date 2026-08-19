/// <summary>
/// Window and fullscreen handling.
///
/// Raylib does not resize the framebuffer when toggling fullscreen, so simply
/// calling ToggleFullscreen leaves a 1280x720 render target stretched across
/// the monitor. The window must be resized to the monitor before entering and
/// back to the windowed size after leaving.
/// </summary>
public static class Display
{
    /// <summary>Applies the settings' fullscreen flag to the actual window.</summary>
    public static void Apply(GameSettings settings)
    {
        if (settings.IsFullScreen == Raylib.IsWindowFullscreen()) return;

        if (settings.IsFullScreen) EnterFullscreen(settings);
        else LeaveFullscreen(settings);
    }

    /// <summary>Flips the flag and applies it. Settings keep the windowed size.</summary>
    public static void ToggleFullscreen(GameSettings settings)
    {
        settings.IsFullScreen = !settings.IsFullScreen;
        Apply(settings);
    }

    private static void EnterFullscreen(GameSettings settings)
    {
        // Remember the size to come back to before the window is resized away.
        settings.Width = Raylib.GetScreenWidth();
        settings.Height = Raylib.GetScreenHeight();

        int monitor = Raylib.GetCurrentMonitor();
        Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
        Raylib.ToggleFullscreen();
    }

    private static void LeaveFullscreen(GameSettings settings)
    {
        Raylib.ToggleFullscreen();
        Raylib.SetWindowSize(settings.Width, settings.Height);
    }
}
