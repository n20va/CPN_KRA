using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using CpnLab.Domain;
using CpnLab.Engine;

namespace CpnLab.Ui2;

public partial class MainWindow : Window
{
    private sealed record PlaceRow(PetriNet<int>.Place Place, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record TransitionRow(PetriNet<int>.Transition Transition, string Text, bool Enabled)
    {
        public override string ToString() => Text;
    }

    private PetriNet<int> _net = null!;
    private Marking<int> _marking = null!;
    private readonly Simulator<int> _simulator = new();
    private readonly ObservableCollection<string> _audit = new();
    private PlaceRow? _selectedPlace;
    private TransitionRow? _selectedTransition;
    private readonly Random _rng = new();
    private const int DefaultInputs = 5;
    private const int MaxInputs = 30;

    public MainWindow()
    {
        InitializeComponent();
        BuildNet(DefaultInputs);
        ResetMarking();

        _simulator.TransitionFired += e =>
        {
            AddAudit($"fired {e.TransitionId}");

            foreach (var (place, token, count) in e.Consumed)
                AddAudit($"- {place}: token {token} -= {count}");

            foreach (var (place, token, count) in e.Produced)
                AddAudit($"+ {place}: token {token} += {count}");
        };

        LbAudit.ItemsSource = _audit;

        RefreshAll();
        SetStatus("Ready.");
    }

    private void BuildNet(int inputsCount)
    {
        if (inputsCount < 1) inputsCount = 1;
        if (inputsCount > MaxInputs) inputsCount = MaxInputs;

        var inputs = Enumerable.Range(1, inputsCount)
            .Select(i => new PetriNet<int>.Place(new PlaceId($"IN{i}"), $"Input {i}"))
            .ToList();
        var output = new PetriNet<int>.Place(new PlaceId("OUT"), "Output");
        var transitions = new List<PetriNet<int>.Transition>();

        for (int i = 1; i <= inputsCount; i++)
            transitions.Add(new PetriNet<int>.Transition(new TransitionId($"T{i}"), $"Move Input {i} → Out"));

        var tMerge = new PetriNet<int>.Transition(new TransitionId("TM"), $"Merge {inputsCount} inputs → Out");
        transitions.Add(tMerge);

        _net = new PetriNet<int>(
            places: inputs.Concat(new[] { output }),
            transitions: transitions
        );

        for (int i = 1; i <= inputsCount; i++)
        {
            var p = inputs[i - 1];
            var ti = new TransitionId($"T{i}");

            _net.AddInputArc(p.Id, ti, Ms((i, 1)));
            _net.AddOutputArc(ti, output.Id, Ms((100 + i, 1)));
        }

        for (int i = 1; i <= inputsCount; i++)
        {
            var p = inputs[i - 1];
            _net.AddInputArc(p.Id, tMerge.Id, Ms((i, 1)));
        }

        _net.AddOutputArc(tMerge.Id, output.Id, Ms((999, 1)));
    }

    private void ResetMarking()
    {
        _marking = new Marking<int>();

        var inputs = _net.Places.Where(p => p.Name.StartsWith("Input ")).ToList();
        for (int i = 0; i < inputs.Count; i++)
        {
            var p = inputs[i];
            _marking[p.Id].Clear();
            _marking[p.Id].Add(i + 1, 1);
        }

        var outPlace = _net.Places.First(p => p.Name == "Output");
        _marking[outPlace.Id].Clear();

        _audit.Clear();
        AddAudit("reset");

        _selectedPlace = null;
        _selectedTransition = null;
    }

    private static Multiset<int> Ms(params (int token, int count)[] items)
    {
        var ms = new Multiset<int>();
        foreach (var (t, c) in items)
            ms.Add(t, c);
        return ms;
    }

    private void OnBuildNet(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TbInputsCount.Text, out var n))
            n = DefaultInputs;

        n = Math.Min(Math.Max(n, 1), MaxInputs);

        BuildNet(n);
        ResetMarking();
        RefreshAll();

        SetStatus($"Built net with {n} inputs.");
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        ResetMarking();
        RefreshAll();
        SetStatus("Reset done.");
    }

    private void OnFireSelected(object? sender, RoutedEventArgs e)
    {
        if (_selectedTransition == null || !_selectedTransition.Enabled)
        {
            SetStatus("Select enabled transition.");
            return;
        }

        if (!_simulator.TryFire(_net, _marking, _selectedTransition.Transition.Id, out var error))
        {
            SetStatus(error ?? "Fire failed.");
            return;
        }

        RefreshAll();
    }

    private void OnStepRandom(object? sender, RoutedEventArgs e)
    {
        var enabled = _simulator.GetEnabledTransitions(_net, _marking);
        if (enabled.Count == 0)
        {
            SetStatus("No enabled transitions.");
            return;
        }

        var tid = enabled[_rng.Next(enabled.Count)];
        _simulator.TryFire(_net, _marking, tid, out _);
        RefreshAll();
    }

    private void OnFireAll(object? sender, RoutedEventArgs e)
    {
        var fired = _simulator.FireAllEnabled(_net, _marking);

        if (fired.Count == 0)
            SetStatus("No transitions fired.");
        else
            SetStatus($"Fired {fired.Count} transitions.");

        AddAudit("fire-all: " + (fired.Count == 0 ? "(none)" : string.Join(", ", fired)));
        RefreshAll();
    }

    private void OnPlaceSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedPlace = LbPlaces.SelectedItem as PlaceRow;
    }

    private void OnTransitionSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedTransition = LbTransitions.SelectedItem as TransitionRow;
        BtnFireSelected.IsEnabled = _selectedTransition?.Enabled == true;
    }

    private void OnApplyTokens(object? sender, RoutedEventArgs e)
    {
        if (_selectedPlace == null)
        {
            SetStatus("Select a place first.");
            return;
        }

        if (!int.TryParse(TbTokenValue.Text, out var token) ||
            !int.TryParse(TbTokenCount.Text, out var count) ||
            token < 0 || count < 0)
        {
            SetStatus("Invalid token or count.");
            return;
        }

        _marking[_selectedPlace.Place.Id].Clear();
        if (count > 0)
            _marking[_selectedPlace.Place.Id].Add(token, count);

        AddAudit($"set {_selectedPlace.Place.Name}: token {token} = {count}");
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshPlaces();
        RefreshTransitions();
        RefreshGraph();
    }

    private void RefreshPlaces()
    {
        var selectedId = _selectedPlace?.Place.Id;

        var rows = _net.Places
            .Select(p => new PlaceRow(p, $"{p.Name}: {FormatMultiset(_marking[p.Id])}"))
            .ToList();

        LbPlaces.ItemsSource = rows;

        if (selectedId != null)
        {
            var idx = rows.FindIndex(r => r.Place.Id.Equals(selectedId));
            if (idx >= 0)
            {
                LbPlaces.SelectedIndex = idx;
                _selectedPlace = rows[idx];
            }
            else _selectedPlace = null;
        }
        else _selectedPlace = null;
    }

    private void RefreshTransitions()
    {
        var enabledSet = _simulator.GetEnabledTransitions(_net, _marking).ToHashSet();

        var rows = _net.Transitions
            .Select(t =>
            {
                var isEnabled = enabledSet.Contains(t.Id);
                return new TransitionRow(
                    t,
                    $"{(isEnabled ? "Enabled " : "Not Enabled")} {t.Id} — {t.Name}",
                    isEnabled
                );
            })
            .ToList();

        var selectedId = _selectedTransition?.Transition.Id;

        LbTransitions.ItemsSource = rows;

        if (selectedId != null)
        {
            var idx = rows.FindIndex(r => r.Transition.Id.Equals(selectedId));
            if (idx >= 0)
            {
                LbTransitions.SelectedIndex = idx;
                _selectedTransition = rows[idx];
            }
            else _selectedTransition = null;
        }
        else _selectedTransition = null;

        TxtEnabled.Text = enabledSet.Count == 0
            ? "Enabled: (none)"
            : "Enabled: " + string.Join(", ", enabledSet);

        BtnFireSelected.IsEnabled = _selectedTransition?.Enabled == true;
    }

    private static string FormatMultiset(Multiset<int> ms)
    {
        if (ms.TotalCount == 0)
            return "empty";

        return string.Join(", ", ms.Select(kv => $"token {kv.Key} = {kv.Value}"));
    }

    private void AddAudit(string text)
    {
        _audit.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {text}");
    }

    private void SetStatus(string text)
    {
        TxtStatus.Text = text;
    }

    private void RefreshGraph()
    {
        NetCanvas.Children.Clear();

        var inputs = _net.Places.Where(p => p.Name.StartsWith("Input ")).ToList();
        var output = _net.Places.First(p => p.Name == "Output");
        var enabledSet = _simulator.GetEnabledTransitions(_net, _marking).ToHashSet();

        const double placeR = 20;
        const double leftX = 80;
        const double midX = 260;
        const double rightX = 450;

        const double topY = 50;
        var spacing = inputs.Count <= 1 ? 0 : Math.Min(60.0, 420.0 / (inputs.Count - 1));

        var inPos = new Dictionary<PlaceId, (double x, double y)>();
        for (int i = 0; i < inputs.Count; i++)
        {
            var y = topY + i * spacing;
            inPos[inputs[i].Id] = (leftX, y);
        }

        var midY = inputs.Count == 0 ? 250.0 : topY + ((inputs.Count - 1) * spacing) / 2.0;
        var outPos = (x: rightX, y: midY);

        foreach (var p in inputs)
        {
            var (x, y) = inPos[p.Id];
            DrawPlace(x, y, placeR, p.Name, _marking[p.Id].TotalCount.ToString());
        }

        DrawPlace(outPos.x, outPos.y, placeR, output.Name, _marking[output.Id].TotalCount.ToString());

        var transitions = _net.Transitions.ToList();
        var byId = transitions.ToDictionary(t => t.Id.Value, t => t);

        string NeedLabelForSingle(int token) => $"need: token {token} ×1";
        string OutLabelForSingle(int token) => $"out: token {token} ×1";

        for (int i = 1; i <= inputs.Count; i++)
        {
            var tid = $"T{i}";
            if (!byId.TryGetValue(tid, out var t)) continue;

            var (ix, iy) = inPos[inputs[i - 1].Id];

            var tx = midX;
            var ty = iy;

            DrawTransition(tx, ty, 70, 30, tid, enabledSet.Contains(t.Id));

            DrawLine(ix + placeR, iy, tx - 5, ty);
            DrawText(NeedLabelForSingle(i), (ix + tx) / 2, iy - 16);

            DrawLine(tx + 75, ty, outPos.x - placeR, outPos.y);
            DrawText(OutLabelForSingle(100 + i), (tx + outPos.x) / 2, (ty + outPos.y) / 2 - 16);
        }

        if (byId.TryGetValue("TM", out var tm))
        {
            var tx = midX;
            var ty = midY;

            DrawTransition(tx, ty, 80, 34, "TM", enabledSet.Contains(tm.Id));

            foreach (var p in inputs)
            {
                var (ix, iy) = inPos[p.Id];
                DrawLine(ix + placeR, iy, tx - 5, ty);

                var inputNumber = ParseInputNumber(p.Name);
                DrawText($"need: token {inputNumber} ×1", (ix + tx) / 2, iy - 16);
            }

            DrawLine(tx + 85, ty, outPos.x - placeR, outPos.y);
            DrawText("out: token 999 ×1", (tx + outPos.x) / 2, (ty + outPos.y) / 2 - 16);
        }


        int ParseInputNumber(string placeName)
        {
            var parts = placeName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[1], out var n)) return n;
            return 0;
        }

        void DrawPlace(double cx, double cy, double r, string name, string tokensTotal)
        {
            var ellipse = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Canvas.SetLeft(ellipse, cx - r);
            Canvas.SetTop(ellipse, cy - r);
            NetCanvas.Children.Add(ellipse);

            var tok = new TextBlock
            {
                Text = tokensTotal,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            };
            Canvas.SetLeft(tok, cx - 6);
            Canvas.SetTop(tok, cy - 10);
            NetCanvas.Children.Add(tok);

            var label = new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = Brushes.White
            };
            Canvas.SetLeft(label, cx - r);
            Canvas.SetTop(label, cy + r + 4);
            NetCanvas.Children.Add(label);
        }

        void DrawTransition(double x, double y, double w, double h, string idText, bool enabled)
        {
            var rect = new Rectangle
            {
                Width = w,
                Height = h,
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Fill = enabled ? Brushes.LightGreen : Brushes.IndianRed
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y - h / 2);
            NetCanvas.Children.Add(rect);

            var label = new TextBlock
            {
                Text = idText,
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(label, x + 10);
            Canvas.SetTop(label, y - 9);
            NetCanvas.Children.Add(label);
        }

        void DrawLine(double x1, double y1, double x2, double y2)
        {
            var line = new Line
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                Stroke = Brushes.Gray,
                StrokeThickness = 2
            };
            NetCanvas.Children.Add(line);
        }

        void DrawText(string text, double x, double y)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = Brushes.White
            };
            Canvas.SetLeft(tb, x - 55);
            Canvas.SetTop(tb, y);
            NetCanvas.Children.Add(tb);
        }
    }

}
