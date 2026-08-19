/// <summary>
/// Every tunable number in the game. Adding a member here is all that is
/// required to make a new stat upgradeable - StatBlock needs no change.
/// </summary>
public enum StatId
{
    // Weapon
    Damage,
    FireRate,
    BulletSpeed,
    BulletLifetime,
    ProjectileCount,
    Spread,
    ExplosionRadius,

    // Actor
    MoveSpeed,
    MaxHealth,
    Health,
    ContactDamage,

    // Enemy
    CoinValue,
    ScoreValue,
    SpawnInterval,
    SpawnCount,
    ProximityRadius,
    StandoffDistance,
}
