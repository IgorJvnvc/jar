namespace PoolTracker.Api.Features.Players.Contracts;

public sealed record PlayerListItemResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    string AvatarColorHex,
    int Points,
    int DebtPoints,
    string? Title
);
