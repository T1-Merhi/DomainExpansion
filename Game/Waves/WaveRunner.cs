public enum WavePhase
{
    /// <summary>Releasing this wave's enemies in batches.</summary>
    Spawning,

    /// <summary>Everything is out; waiting for the arena to be cleared.</summary>
    Active,

    /// <summary>Between waves. The shop is the point of this phase.</summary>
    Resting,
}

/// <summary>
/// Drives the endless wave loop: spawn, clear, rest, repeat.
///
/// Spawning is paced in batches rather than dumped at once, so a wave arrives
/// as pressure that builds instead of a wall that appears.
/// </summary>
public sealed class WaveRunner
{
    private readonly World _world;
    private readonly WaveGenerator _generator;

    private int _remainingChaser;
    private int _remainingShooter;
    private int _remainingSpawner;

    private int _spawnCooldown;
    private int _restTicks;

    public WaveRunner(World world, WaveGenerator generator)
    {
        _world = world;
        _generator = generator;

        WaveNumber = 0;
        Phase = WavePhase.Resting;

        // A short opening rest, so the player is not fighting before the window
        // has settled and can reach the shop first.
        _restTicks = World.TickRate * 3;
    }

    public int WaveNumber { get; private set; }
    public WavePhase Phase { get; private set; }

    public int RestTicksRemaining => _restTicks;
    public float RestSecondsRemaining => _restTicks / (float)World.TickRate;

    public int RemainingToSpawn => _remainingChaser + _remainingShooter + _remainingSpawner;

    public void Tick()
    {
        switch (Phase)
        {
            case WavePhase.Resting: TickResting(); break;
            case WavePhase.Spawning: TickSpawning(); break;
            case WavePhase.Active: TickActive(); break;
        }
    }

    /// <summary>Ends the rest early. Ignored outside the rest phase.</summary>
    public void SkipRest()
    {
        if (Phase == WavePhase.Resting) _restTicks = 0;
    }

    /// <summary>Jumps straight to a wave. Used by the test arena in M6.</summary>
    public void StartWave(int wave)
    {
        WaveNumber = Math.Max(1, wave) - 1;
        _restTicks = 0;
        Phase = WavePhase.Resting;
    }

    private void TickResting()
    {
        if (_restTicks > 0)
        {
            _restTicks--;
            return;
        }

        BeginNextWave();
    }

    private void BeginNextWave()
    {
        WaveNumber++;

        WaveComposition composition = _generator.For(WaveNumber);
        _remainingChaser = composition.Chaser;
        _remainingShooter = composition.Shooter;
        _remainingSpawner = composition.Spawner;

        _spawnCooldown = 0;
        Phase = WavePhase.Spawning;
    }

    private void TickSpawning()
    {
        if (_spawnCooldown > 0)
        {
            _spawnCooldown--;
            return;
        }

        int batch = Math.Max(1, _generator.Config.SpawnBatchSize);

        for (int i = 0; i < batch && RemainingToSpawn > 0; i++)
        {
            SpawnOne();
        }

        if (RemainingToSpawn == 0)
        {
            Phase = WavePhase.Active;
            return;
        }

        _spawnCooldown = Math.Max(1,
            (int)MathF.Round(_generator.Config.SpawnIntervalSeconds * World.TickRate));
    }

    /// <summary>
    /// Spawners first, then shooters, then chasers. Getting the support types
    /// out early means the wave has structure rather than a chaser rush
    /// followed by stragglers.
    /// </summary>
    private void SpawnOne()
    {
        EnemyType type;

        if (_remainingSpawner > 0) { type = EnemyType.Spawner; _remainingSpawner--; }
        else if (_remainingShooter > 0) { type = EnemyType.Shooter; _remainingShooter--; }
        else if (_remainingChaser > 0) { type = EnemyType.Chaser; _remainingChaser--; }
        else return;

        _world.SpawnEnemy(type, _world.RandomEdgePosition());
    }

    /// <summary>
    /// The wave ends only when the arena is empty. Spawner offspring live in
    /// the same pool, so a surviving spawner keeps the wave alive by itself -
    /// which is exactly the pressure it is meant to apply.
    /// </summary>
    private void TickActive()
    {
        if (_world.Enemies.ActiveCount > 0) return;

        _restTicks = Math.Max(1, (int)MathF.Round(_generator.Config.RestSeconds * World.TickRate));
        Phase = WavePhase.Resting;
    }
}
