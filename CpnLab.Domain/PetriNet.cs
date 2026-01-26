namespace CpnLab.Domain;

public sealed class PetriNet<T> where T : notnull
{
    public sealed record Place(PlaceId Id, string Name);
    public sealed record Transition(TransitionId Id, string Name);

    private readonly Dictionary<(PlaceId, TransitionId), Multiset<T>> _inputs = new();
    private readonly Dictionary<(TransitionId, PlaceId), Multiset<T>> _outputs = new();
    private readonly Dictionary<TransitionId, Dictionary<PlaceId, Multiset<T>>> _inByT = new();
    private readonly Dictionary<TransitionId, Dictionary<PlaceId, Multiset<T>>> _outByT = new();

    public IReadOnlyList<Place> Places { get; }
    public IReadOnlyList<Transition> Transitions { get; }

    public PetriNet(IEnumerable<Place> places, IEnumerable<Transition> transitions)
    {
        Places = places.ToList();
        Transitions = transitions.ToList();

        if (Places.Select(p => p.Id).Distinct().Count() != Places.Count)
            throw new ArgumentException("Duplicate place ids.");

        if (Transitions.Select(t => t.Id).Distinct().Count() != Transitions.Count)
            throw new ArgumentException("Duplicate transition ids.");
    }

    private static Dictionary<PlaceId, Multiset<T>> GetOrCreate(
        Dictionary<TransitionId, Dictionary<PlaceId, Multiset<T>>> dict,
        TransitionId tid)
    {
        if (!dict.TryGetValue(tid, out var m))
        {
            m = new Dictionary<PlaceId, Multiset<T>>();
            dict[tid] = m;
        }

        return m;
    }

    public void AddInputArc(PlaceId place, TransitionId transition, Multiset<T> requiredTokens)
    {
        if (requiredTokens == null)
            throw new ArgumentNullException(nameof(requiredTokens));

        _inputs[(place, transition)] = requiredTokens;

        var map = GetOrCreate(_inByT, transition);
        map[place] = requiredTokens;
    }

    public void AddOutputArc(TransitionId transition, PlaceId place, Multiset<T> producedTokens)
    {
        if (producedTokens == null)
            throw new ArgumentNullException(nameof(producedTokens));

        _outputs[(transition, place)] = producedTokens;

        var map = GetOrCreate(_outByT, transition);
        map[place] = producedTokens;
    }
    
    public Multiset<T> GetInput(PlaceId place, TransitionId transition)
        => _inputs.TryGetValue((place, transition), out var ms) ? ms : new Multiset<T>();

    public Multiset<T> GetOutput(TransitionId transition, PlaceId place)
        => _outputs.TryGetValue((transition, place), out var ms) ? ms : new Multiset<T>();

    public IReadOnlyList<(PlaceId place, Multiset<T> ms)> GetInputsOf(TransitionId transition)
    {
        if (!_inByT.TryGetValue(transition, out var map) || map.Count == 0)
            return Array.Empty<ValueTuple<PlaceId, Multiset<T>>>();

        return map.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public IReadOnlyList<(PlaceId place, Multiset<T> ms)> GetOutputsOf(TransitionId transition)
    {
        if (!_outByT.TryGetValue(transition, out var map) || map.Count == 0)
            return Array.Empty<ValueTuple<PlaceId, Multiset<T>>>();

        return map.Select(kv => (kv.Key, kv.Value)).ToList();
    }
}
