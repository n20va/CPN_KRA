using System;
using System.Linq;
using CpnLab.Domain;
using CpnLab.Engine;
using Xunit;

public class SimulatorTests
{
    private static Multiset<int> Ms(params (int token, int count)[] items)
    {
        var ms = new Multiset<int>();
        foreach (var (t, c) in items)
            ms.Add(t, c);
        return ms;
    }

    [Fact]
    public void Transition_is_enabled_when_required_tokens_exist()
    {
        var p1 = new PetriNet<int>.Place(new PlaceId("P1"), "Input");
        var p2 = new PetriNet<int>.Place(new PlaceId("P2"), "Output");
        var t1 = new PetriNet<int>.Transition(new TransitionId("T1"), "Convert");
        var net = new PetriNet<int>(new[] { p1, p2 }, new[] { t1 });
        net.AddInputArc(p1.Id, t1.Id, Ms((1, 1)));
        net.AddOutputArc(t1.Id, p2.Id, Ms((2, 1)));
        var marking = new Marking<int>();
        marking[p1.Id].Add(1, 1);
        var simulator = new Simulator<int>();
        var enabled = simulator.GetEnabledTransitions(net, marking);

        Assert.Contains(t1.Id, enabled);
    }

    [Fact]
    public void Fire_moves_tokens_from_input_to_output()
    {
        var p1 = new PetriNet<int>.Place(new PlaceId("P1"), "Input");
        var p2 = new PetriNet<int>.Place(new PlaceId("P2"), "Output");
        var t1 = new PetriNet<int>.Transition(new TransitionId("T1"), "Convert");
        var net = new PetriNet<int>(new[] { p1, p2 }, new[] { t1 });
        net.AddInputArc(p1.Id, t1.Id, Ms((1, 1)));
        net.AddOutputArc(t1.Id, p2.Id, Ms((2, 1)));
        var marking = new Marking<int>();
        marking[p1.Id].Add(1, 1);
        var simulator = new Simulator<int>();
        simulator.Fire(net, marking, t1.Id);
        Assert.Equal(0, marking[p1.Id].GetCount(1));
        Assert.Equal(1, marking[p2.Id].GetCount(2));
    }

    [Fact]
    public void Fire_throws_if_transition_is_not_enabled()
    {
        var p1 = new PetriNet<int>.Place(new PlaceId("P1"), "Input");
        var p2 = new PetriNet<int>.Place(new PlaceId("P2"), "Output");
        var t1 = new PetriNet<int>.Transition(new TransitionId("T1"), "Convert");
        var net = new PetriNet<int>(new[] { p1, p2 }, new[] { t1 });
        net.AddInputArc(p1.Id, t1.Id, Ms((1, 1)));
        var marking = new Marking<int>();
        var simulator = new Simulator<int>();
        Assert.Throws<InvalidOperationException>(() =>
            simulator.Fire(net, marking, t1.Id));
    }


    [Fact]
    public void Transition_requires_tokens_from_two_places_AND_condition()
    {
        var p1 = new PetriNet<int>.Place(new PlaceId("P1"), "A");
        var p2 = new PetriNet<int>.Place(new PlaceId("P2"), "B");
        var p3 = new PetriNet<int>.Place(new PlaceId("P3"), "C");
        var t1 = new PetriNet<int>.Transition(new TransitionId("T1"), "AND");
        var net = new PetriNet<int>(new[] { p1, p2, p3 }, new[] { t1 });
        net.AddInputArc(p1.Id, t1.Id, Ms((1, 1)));
        net.AddInputArc(p2.Id, t1.Id, Ms((2, 1)));
        net.AddOutputArc(t1.Id, p3.Id, Ms((3, 1)));
        var sim = new Simulator<int>();
        var m = new Marking<int>();

        m[p1.Id].Add(1, 1);
        Assert.DoesNotContain(t1.Id, sim.GetEnabledTransitions(net, m));

        m[p2.Id].Add(2, 1);
        Assert.Contains(t1.Id, sim.GetEnabledTransitions(net, m));

        sim.Fire(net, m, t1.Id);
        Assert.Equal(0, m[p1.Id].GetCount(1));
        Assert.Equal(0, m[p2.Id].GetCount(2));
        Assert.Equal(1, m[p3.Id].GetCount(3));
    }

    [Fact]
    public void TryFire_returns_false_instead_of_throw_when_not_enabled()
    {
        var p1 = new PetriNet<int>.Place(new PlaceId("P1"), "Input");
        var t1 = new PetriNet<int>.Transition(new TransitionId("T1"), "Convert");
        var net = new PetriNet<int>(new[] { p1 }, new[] { t1 });
        net.AddInputArc(p1.Id, t1.Id, Ms((1, 1)));
        var m = new Marking<int>();
        var sim = new Simulator<int>();
        var ok = sim.TryFire(net, m, t1.Id, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("not enabled", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parallel_enabled_transitions_equal_to_sequential()
    {
        var p1 = new PetriNet<int>.Place(new PlaceId("P1"), "Input");
        var p2 = new PetriNet<int>.Place(new PlaceId("P2"), "Output");
        var t1 = new PetriNet<int>.Transition(new TransitionId("T1"), "A");
        var t2 = new PetriNet<int>.Transition(new TransitionId("T2"), "B");
        var t3 = new PetriNet<int>.Transition(new TransitionId("T3"), "C");
        var net = new PetriNet<int>(new[] { p1, p2 }, new[] { t1, t2, t3 });
        net.AddInputArc(p1.Id, t1.Id, Ms((1, 1)));
        net.AddOutputArc(t1.Id, p2.Id, Ms((2, 1)));
        net.AddInputArc(p1.Id, t2.Id, Ms((1, 1)));
        net.AddOutputArc(t2.Id, p2.Id, Ms((2, 1)));
        net.AddInputArc(p1.Id, t3.Id, Ms((999, 1)));
        var m = new Marking<int>();
        m[p1.Id].Add(1, 1);
        var sim = new Simulator<int>();
        var seq = sim.GetEnabledTransitions(net, m).ToHashSet();
        var par = sim.GetEnabledTransitionsParallel(net, m).ToHashSet();

        Assert.True(seq.SetEquals(par));
        Assert.Contains(t1.Id, par);
        Assert.Contains(t2.Id, par);
        Assert.DoesNotContain(t3.Id, par);
    }

    [Fact]
    public void FireAllEnabled_fires_only_non_conflicting_transitions()
    {
        var pIn = new PetriNet<int>.Place(new PlaceId("PIN"), "Input");
        var pOut = new PetriNet<int>.Place(new PlaceId("POUT"), "Output");
        var t1 = new PetriNet<int>.Transition(new TransitionId("T1"), "Take token");
        var t2 = new PetriNet<int>.Transition(new TransitionId("T2"), "Take token too");
        var net = new PetriNet<int>(new[] { pIn, pOut }, new[] { t1, t2 });
        net.AddInputArc(pIn.Id, t1.Id, Ms((1, 1)));
        net.AddOutputArc(t1.Id, pOut.Id, Ms((10, 1)));
        net.AddInputArc(pIn.Id, t2.Id, Ms((1, 1)));
        net.AddOutputArc(t2.Id, pOut.Id, Ms((20, 1)));
        var m = new Marking<int>();
        m[pIn.Id].Add(1, 1);
        var sim = new Simulator<int>();
        var fired = sim.FireAllEnabled(net, m);

        Assert.True(fired.Count == 1, "Only one transition can fire due to conflict.");
        Assert.Equal(0, m[pIn.Id].GetCount(1));
        
        var out10 = m[pOut.Id].GetCount(10);
        var out20 = m[pOut.Id].GetCount(20);

        Assert.True(out10 + out20 == 1, "Exactly one output token must be produced.");
    }
}