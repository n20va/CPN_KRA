using CpnLab.Domain;
using CpnLab.Engine;
using System.Diagnostics;


static Multiset<int> Ms(params (int token, int count)[] items)
{
    var ms = new Multiset<int>();
    foreach (var (t, c) in items)
        ms.Add(t, c);
    return ms;
}


var p1 = new PetriNet<int>.Place(new PlaceId("P1"), "Input");
var p2 = new PetriNet<int>.Place(new PlaceId("P2"), "Output");

var t1 = new PetriNet<int>.Transition(
    new TransitionId("T1"),
    "Convert 1 -> 2"
);

var net = new PetriNet<int>(
    new[] { p1, p2 },
    new[] { t1 }
);

net.AddInputArc(p1.Id, t1.Id, Ms((1, 1)));
net.AddOutputArc(t1.Id, p2.Id, Ms((2, 1)));


var marking = new Marking<int>();
marking[p1.Id].Add(1, 1);


var simulator = new Simulator<int>();
simulator.TransitionFired += e =>
{
    Console.WriteLine(
        $"[AUDIT] {e.TimestampUtc:O} | fired {e.TransitionId}"
    );
};


Console.WriteLine("Enabled transitions:");
foreach (var tid in simulator.GetEnabledTransitions(net, marking))
    Console.WriteLine($" - {tid}");

Console.WriteLine("Firing T1...");
simulator.Fire(net, marking, t1.Id);

Console.WriteLine($"P1: token 1 count = {marking[p1.Id].GetCount(1)}");
Console.WriteLine($"P2: token 2 count = {marking[p2.Id].GetCount(2)}");


static PetriNet<int> BuildBenchmarkNet(int transitionsCount)
{
    var pIn = new PetriNet<int>.Place(new PlaceId("BIN"), "BenchInput");
    var pOut = new PetriNet<int>.Place(new PlaceId("BOUT"), "BenchOutput");
    var transitions = new List<PetriNet<int>.Transition>();
    for (var i = 0; i < transitionsCount; i++)
        transitions.Add(new PetriNet<int>.Transition(new TransitionId($"BT{i}"), $"BenchT{i}"));

    var net = new PetriNet<int>(new[] { pIn, pOut }, transitions);

    foreach (var t in transitions)
    {
        var req = new Multiset<int>();
        req.Add(1, 1);

        var prod = new Multiset<int>();
        prod.Add(2, 1);

        net.AddInputArc(pIn.Id, t.Id, req);
        net.AddOutputArc(t.Id, pOut.Id, prod);
    }

    return net;
}

static Marking<int> BuildBenchmarkMarking()
{
    var m = new Marking<int>();
    m[new PlaceId("BIN")].Add(1, 1); 
    return m;
}

static void Measure(string title, Action action, int iterations)
{
    action();

    var sw = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++)
        action();
    sw.Stop();

    Console.WriteLine($"{title}: {sw.ElapsedMilliseconds} ms for {iterations} iterations");
}

Console.WriteLine();
Console.WriteLine("Benchmark: enabled transitions");

var benchNet = BuildBenchmarkNet(transitionsCount: 50_000);
var benchMarking = BuildBenchmarkMarking();
var benchSimulator = new Simulator<int>();

Measure("Sequential GetEnabledTransitions",
    () => benchSimulator.GetEnabledTransitions(benchNet, benchMarking),
    iterations: 200);
Measure("Parallel GetEnabledTransitionsParallel",
    () => benchSimulator.GetEnabledTransitionsParallel(benchNet, benchMarking),
    iterations: 200);

static PetriNet<int> BuildLayeredDagNet(int layers, int width, int fanInPerTransition)
{
    var places = new List<PetriNet<int>.Place>();
    for (int l = 0; l <= layers; l++)
    for (int i = 0; i < width; i++)
        places.Add(new PetriNet<int>.Place(new PlaceId($"L{l}P{i}"), $"L{l}P{i}"));

    var transitions = new List<PetriNet<int>.Transition>();
    for (int l = 0; l < layers; l++)
    for (int i = 0; i < width; i++)
        transitions.Add(new PetriNet<int>.Transition(new TransitionId($"L{l}T{i}"), $"L{l}T{i}"));

    var net = new PetriNet<int>(places, transitions);
    for (int l = 0; l < layers; l++)
    {
        for (int i = 0; i < width; i++)
        {
            var tId = new TransitionId($"L{l}T{i}");
            for (int k = 0; k < fanInPerTransition; k++)
            {
                int srcIndex = (i + k) % width;
                var pSrc = new PlaceId($"L{l}P{srcIndex}");
                var req = new Multiset<int>();
                req.Add(1, 1);
                net.AddInputArc(pSrc, tId, req);
            }

            var pDst = new PlaceId($"L{l+1}P{i}");
            var prod = new Multiset<int>();
            prod.Add(1, 1);
            net.AddOutputArc(tId, pDst, prod);
        }
    }

    return net;
}

static Marking<int> BuildLayeredDagMarking(int width, int tokensPerPlaceOnLayer0)
{
    var m = new Marking<int>();
    for (int i = 0; i < width; i++)
        m[new PlaceId($"L0P{i}")].Add(1, tokensPerPlaceOnLayer0);
    return m;
}

Console.WriteLine();
Console.WriteLine(" Benchmark: enabled transitions (Layered DAG)");

var benchNet0 = BuildLayeredDagNet(layers: 8, width: 2000, fanInPerTransition: 3);
var benchMarking0 = BuildLayeredDagMarking(width: 2000, tokensPerPlaceOnLayer0: 1);
var benchSimulator0 = new Simulator<int>();

Measure("Sequential GetEnabledTransitions",
    () => benchSimulator.GetEnabledTransitions(benchNet0, benchMarking0),
    iterations: 50);

Measure("Parallel GetEnabledTransitionsParallel",
    () => benchSimulator.GetEnabledTransitionsParallel(benchNet0, benchMarking0),
    iterations: 50);

