/// <summary>
/// A frame's input, snapshotted by the scene and handed to the sim. Keeps
/// World free of Raylib calls so the simulation stays independent of how
/// input happens to be read.
/// </summary>
public struct InputState
{
    public Vector2 MousePosition;
    public Vector2 MoveAxis;      // normalised, -1..1 per axis
    public bool FireHeld;
    public int WheelDelta;
}
