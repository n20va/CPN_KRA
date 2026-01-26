using System.Collections;
using System.Diagnostics;

namespace CpnLab.Domain;

[DebuggerDisplay("Unique = {UniqueCount}, Total = {TotalCount}")]
public sealed class Multiset<T> : IEnumerable<KeyValuePair<T, int>> where T : notnull
{
    private readonly Dictionary<T, int> _items = new();

    public int UniqueCount => _items.Count;
    public int TotalCount => _items.Values.Sum();

    public int GetCount(T item) => _items.TryGetValue(item, out var c) ? c : 0;

    public bool Contains(T item, int count = 1) => count > 0 && GetCount(item) >= count;

    public void Add(T item, int count = 1)
    {
        if (count <= 0) return;
        _items[item] = GetCount(item) + count;
    }

    public void Remove(T item, int count = 1)
    {
        if (count <= 0) return;

        var cur = GetCount(item);
        var next = cur - count;

        if (next <= 0) _items.Remove(item);
        else _items[item] = next;
    }

    public void Clear() => _items.Clear();

    public Multiset<T> Clone()
    {
        var copy = new Multiset<T>();
        foreach (var kv in _items)
            copy._items[kv.Key] = kv.Value;
        return copy;
    }

    public IEnumerator<KeyValuePair<T, int>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}