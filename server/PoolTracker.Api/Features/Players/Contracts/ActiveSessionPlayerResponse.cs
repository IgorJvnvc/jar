namespace PoolTracker.Api.Features.Players.Contracts;

public sealed record ActiveSessionPlayerResponse(
    Guid UserId,
    string DisplayName,
    string AvatarColorHex,
    Guid SessionId,
    Guid PoolHallId,
    string PoolHallName,
    Guid? PoolHallTableId,
    string? PoolHallTableLabel,
    DateTimeOffset StartedAtUtc
);
