/// <summary>Player-facing values that were previously literals in Player and World.</summary>
public sealed class PlayerConfig
{
    public float Radius { get; set; } = 21f;
    public float MoveSpeed { get; set; } = 260f;
    public float MaxHealth { get; set; } = 100f;
    public float TurnResponse { get; set; } = 0.35f;
    public int HitFlashTicks { get; set; } = 15;
    public int MuzzleFlashTicks { get; set; } = 4;
    public int MaxSides { get; set; } = 12;
    public float BulletRadius { get; set; } = 4f;
    public float EnemyBulletRadius { get; set; } = 5f;
}

/// <summary>Feedback timings and behaviour tuning that affect feel rather than rules.</summary>
public sealed class EffectsConfig
{
    public int HitShakeTicks { get; set; } = 7;
    public int ExplosionTicks { get; set; } = 12;
    public int FloatingTextTicks { get; set; } = 42;
    public float FloatingTextDamping { get; set; } = 0.94f;
    public float FloatingTextRiseSpeed { get; set; } = 70f;

    public int SpawnerTelegraphTicks { get; set; } = 36;
    public float ChaserShare { get; set; } = 0.7f;
    public float ShooterBandHalfWidth { get; set; } = 40f;
    public float SpawnerBandHalfWidth { get; set; } = 60f;
    public float SpawnRingRadius { get; set; } = 34f;

    public float ShakeCap { get; set; } = 14f;
    public float ShakeDecay { get; set; } = 0.86f;
    public float ShakeOnPlayerHit { get; set; } = 4f;
}

/// <summary>
/// Ambient access to the loaded tuning values.
///
/// Static because these are read from deep inside entity code and pooled
/// structs that have no constructor injection point, and because exactly one
/// set is live at a time. ConfigStore is the only thing that assigns here.
/// </summary>
public static class Tuning
{
    public static PlayerConfig Player { get; private set; } = new();
    public static EffectsConfig Effects { get; private set; } = new();

    public static void Apply(PlayerConfig player, EffectsConfig effects)
    {
        if (player != null)
        {
            // A polygon needs at least three sides, and Mounts is allocated to
            // the ceiling - so an edit outside that range would index out of
            // range in the render loop rather than merely looking wrong.
            //
            // global:: because this class has a Player property of its own,
            // which would otherwise shadow the entity type.
            player.MaxSides = Math.Clamp(player.MaxSides, 3, global::Player.MaxSidesCeiling);

            if (player.Radius <= 0f) player.Radius = 1f;
            if (player.MaxHealth <= 0f) player.MaxHealth = 1f;

            Player = player;
        }

        if (effects != null) Effects = effects;
    }
}
