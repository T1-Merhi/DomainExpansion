public interface IPoolable
{
    /// <summary>Clear state so the instance is safe to hand out again.</summary>
    void Reset();
}

/// <summary>
/// Fixed-slot object pool. Active items are stored contiguously at the front of
/// the backing array so iteration touches no dead entries and never allocates.
///
/// Return during iteration is safe: removal swaps the last active item into the
/// freed slot, so iterate backwards when returning items mid-loop.
/// </summary>
public sealed class Pool<T> where T : class, IPoolable, new()
{
    private T[] _items;

    public Pool(int capacity = 256)
    {
        _items = new T[capacity];
        for (int i = 0; i < capacity; i++) _items[i] = new T();
    }

    public int ActiveCount { get; private set; }
    public int Capacity => _items.Length;

    /// <summary>Active item by index. Valid for 0 &lt;= i &lt; ActiveCount.</summary>
    public T this[int index] => _items[index];

    public T Rent()
    {
        if (ActiveCount == _items.Length) Grow();

        T item = _items[ActiveCount];
        ActiveCount++;
        return item;
    }

    /// <summary>
    /// Returns the active item at <paramref name="index"/>. The last active item
    /// is swapped into its place, so a backwards loop stays correct.
    /// </summary>
    public void ReturnAt(int index)
    {
        int last = ActiveCount - 1;

        _items[index].Reset();

        if (index != last)
        {
            (_items[index], _items[last]) = (_items[last], _items[index]);
        }

        ActiveCount--;
    }

    public void Clear()
    {
        for (int i = 0; i < ActiveCount; i++) _items[i].Reset();
        ActiveCount = 0;
    }

    private void Grow()
    {
        int oldCapacity = _items.Length;
        Array.Resize(ref _items, oldCapacity * 2);
        for (int i = oldCapacity; i < _items.Length; i++) _items[i] = new T();
    }
}
