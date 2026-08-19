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

    /// <summary>Latest input snapshot, written by the scene before ticking.</summary>
    public InputState Input;

    public World(Vector2 playerStart)
    {
        Player.Position = playerStart;
    }

    public void Tick()
    {
        TickCount++;

        Player.AimAt(Input.MousePosition);
    }
}
