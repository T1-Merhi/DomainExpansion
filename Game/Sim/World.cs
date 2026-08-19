/// <summary>
/// The simulation. Advances in fixed steps and holds all game state.
/// Deliberately contains no Raylib drawing calls and takes no delta time:
/// one Tick is always exactly <see cref="TickRate"/> of a second.
/// </summary>
public sealed class World
{
    public const int TickRate = 60;
    public const float FixedStep = 1f / TickRate;

    public long TickCount { get; private set; }

    public readonly Player Player = new();

    /// <summary>Play area in world units. Currently matches the window.</summary>
    public Vector2 ArenaSize { get; private set; }

    /// <summary>Latest input snapshot, written by the scene before ticking.</summary>
    public InputState Input;

    public World(Vector2 arenaSize)
    {
        ArenaSize = arenaSize;
        Player.Position = arenaSize * 0.5f;
    }

    public void Resize(Vector2 arenaSize) => ArenaSize = arenaSize;

    public void Tick()
    {
        TickCount++;

        Player.Move(Input.MoveAxis, ArenaSize);
        Player.AimAt(Input.MousePosition);
    }
}
