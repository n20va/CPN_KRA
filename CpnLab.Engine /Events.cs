using CpnLab.Domain;

namespace CpnLab.Engine;

public sealed record TransitionFiredEvent<T>(
    TransitionId TransitionId,
    DateTimeOffset TimestampUtc,
    IReadOnlyList<(PlaceId Place, T Token, int Count)> Consumed,
    IReadOnlyList<(PlaceId Place, T Token, int Count)> Produced
) where T : notnull;