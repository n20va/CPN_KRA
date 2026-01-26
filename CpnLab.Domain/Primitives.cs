namespace CpnLab.Domain;

public sealed record PlaceId(string Value)
{
    public override string ToString() => Value;
}

public sealed record TransitionId(string Value)
{
    public override string ToString() => Value;
}