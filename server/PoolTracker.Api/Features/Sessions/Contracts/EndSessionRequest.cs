using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Sessions.Contracts;

public sealed class EndSessionRequest
{
    [Range(0, 500)]
    public int BallsPotted { get; init; }

    [Range(0, 100)]
    public int GamesWon { get; init; }

    [Range(0, 100)]
    public int GamesLost { get; init; }

    [Range(0, 100)]
    public int SnookersEscaped { get; init; }

    [MaxLength(750)]
    public string? Notes { get; init; }
}
