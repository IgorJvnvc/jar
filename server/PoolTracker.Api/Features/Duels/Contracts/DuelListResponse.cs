namespace PoolTracker.Api.Features.Duels.Contracts;

public sealed record DuelListResponse(
    IReadOnlyList<DuelStatusResponse> Items
);
