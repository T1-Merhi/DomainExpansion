/// <summary>
/// Uniform grid broad-phase over enemy indices. Rebuilt each tick, which is
/// cheaper than incremental updates when nearly everything moves every tick.
///
/// Bullet-vs-enemy is the hot path: hundreds of bullets against hundreds of
/// enemies is ~100k pair tests naively, and this reduces it to the handful of
/// enemies sharing a bullet's cell.
/// </summary>
public sealed class SpatialGrid
{
    private const float CellSize = 64f;

    private readonly Dictionary<long, List<int>> _cells = new();

    public void Rebuild(Pool<Enemy> enemies)
    {
        foreach (var list in _cells.Values) list.Clear();

        for (int i = 0; i < enemies.ActiveCount; i++)
        {
            long key = KeyFor(enemies[i].Position);

            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<int>(8);
                _cells[key] = list;
            }

            list.Add(i);
        }
    }

    /// <summary>
    /// Invokes <paramref name="action"/> for each enemy index whose cell overlaps
    /// the circle. May report the same index only once, since each enemy sits in
    /// exactly one cell.
    /// </summary>
    public void QueryCircle(Vector2 centre, float radius, Action<int> action)
    {
        int minX = (int)MathF.Floor((centre.X - radius) / CellSize);
        int maxX = (int)MathF.Floor((centre.X + radius) / CellSize);
        int minY = (int)MathF.Floor((centre.Y - radius) / CellSize);
        int maxY = (int)MathF.Floor((centre.Y + radius) / CellSize);

        for (int cy = minY; cy <= maxY; cy++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                if (!_cells.TryGetValue(Key(cx, cy), out var list)) continue;

                for (int i = 0; i < list.Count; i++) action(list[i]);
            }
        }
    }

    private static long KeyFor(Vector2 position) =>
        Key((int)MathF.Floor(position.X / CellSize), (int)MathF.Floor(position.Y / CellSize));

    private static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
}

public static class Collision
{
    public static bool CirclesOverlap(Vector2 a, float ra, Vector2 b, float rb)
    {
        float r = ra + rb;
        return Vector2.DistanceSquared(a, b) <= r * r;
    }
}
