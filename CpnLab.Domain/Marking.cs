namespace CpnLab.Domain;

public sealed class Marking<T> where T : notnull
{
    private readonly Dictionary<PlaceId, Multiset<T>> _byPlace = new();

    public Multiset<T> this[PlaceId place]
    {
        get
        {
            if (!_byPlace.TryGetValue(place, out var ms))
            {
                ms = new Multiset<T>();
                _byPlace[place] = ms;
            }

            return ms;
        }
    }

    public Marking<T> Clone()
    {
        var copy = new Marking<T>();
        foreach (var kv in _byPlace)
            copy._byPlace[kv.Key] = kv.Value.Clone();
        return copy;
    }
}