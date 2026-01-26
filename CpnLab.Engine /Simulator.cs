using System.Collections.Concurrent;
using CpnLab.Domain;

namespace CpnLab.Engine;

public sealed class Simulator<T> where T : notnull
{
    public event Action<TransitionFiredEvent<T>>? TransitionFired;

    public IReadOnlyList<TransitionId> GetEnabledTransitions(
        PetriNet<T> net,
        Marking<T> marking)
    {
        var enabled = new List<TransitionId>();
        foreach (var t in net.Transitions)
        {
            var canFire = true;
            foreach (var (place, reqMs) in net.GetInputsOf(t.Id))
            {
                foreach (var req in reqMs)
                {
                    if (!marking[place].Contains(req.Key, req.Value))
                    {
                        canFire = false;
                        break;
                    }
                }
                if (!canFire)
                    break;
            }
            if (canFire)
                enabled.Add(t.Id);
        }
        return enabled;
    }

    public IReadOnlyList<TransitionId> GetEnabledTransitionsParallel(
        PetriNet<T> net,
        Marking<T> marking)
    {
        var snapshot = marking.Clone();
        var enabled = new ConcurrentBag<TransitionId>();
        Parallel.ForEach(net.Transitions, t =>
        {
            foreach (var (place, reqMs) in net.GetInputsOf(t.Id))
            {
                foreach (var req in reqMs)
                {
                    if (!snapshot[place].Contains(req.Key, req.Value))
                        return;
                }
            }
            enabled.Add(t.Id);
        });
        return enabled.ToList();
    }

    public bool TryFire(
        PetriNet<T> net,
        Marking<T> marking,
        TransitionId transitionId,
        out string? error)
    {
        error = null;
        var enabled = GetEnabledTransitions(net, marking);
        if (!enabled.Contains(transitionId))
        {
            error = $"Transition {transitionId} is not enabled.";
            return false;
        }
        var consumed = new List<(PlaceId Place, T Token, int Count)>();
        var produced = new List<(PlaceId Place, T Token, int Count)>();
        foreach (var (place, input) in net.GetInputsOf(transitionId))
        {
            foreach (var kv in input)
            {
                marking[place].Remove(kv.Key, kv.Value);
                consumed.Add((place, kv.Key, kv.Value));
            }
        }
        foreach (var (place, output) in net.GetOutputsOf(transitionId))
        {
            foreach (var kv in output)
            {
                marking[place].Add(kv.Key, kv.Value);
                produced.Add((place, kv.Key, kv.Value));
            }
        }
        TransitionFired?.Invoke(
            new TransitionFiredEvent<T>(
                transitionId,
                DateTimeOffset.UtcNow,
                consumed,
                produced
            )
        );
        return true;
    }

    public void Fire(
        PetriNet<T> net,
        Marking<T> marking,
        TransitionId transitionId)
    {
        if (!TryFire(net, marking, transitionId, out var error))
            throw new InvalidOperationException(error);
    }

    public IReadOnlyList<TransitionId> FireAllEnabled(
        PetriNet<T> net,
        Marking<T> marking)
    {
        var fired = new List<TransitionId>();
        var virtualMarking = marking.Clone();
        var enabled = GetEnabledTransitions(net, marking);
        foreach (var tid in enabled)
        {
            var canFire = true;
            foreach (var (place, reqMs) in net.GetInputsOf(tid))
            {
                foreach (var req in reqMs)
                {
                    if (!virtualMarking[place].Contains(req.Key, req.Value))
                    {
                        canFire = false;
                        break;
                    }
                }
                if (!canFire) break;
            }
            if (!canFire)
                continue;
            foreach (var (place, reqMs) in net.GetInputsOf(tid))
            foreach (var req in reqMs)
                virtualMarking[place].Remove(req.Key, req.Value);
            foreach (var (place, outMs) in net.GetOutputsOf(tid))
            foreach (var kv in outMs)
                virtualMarking[place].Add(kv.Key, kv.Value);
            fired.Add(tid);
        }
        foreach (var tid in fired)
            TryFire(net, marking, tid, out _);
        return fired;
    }
}
