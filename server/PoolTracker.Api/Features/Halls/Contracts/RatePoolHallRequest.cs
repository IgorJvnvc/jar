using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Halls.Contracts;

public sealed class RatePoolHallRequest
{
    [Range(1, 10)]
    public int TableQuality { get; init; }

    [Range(1, 10)]
    public int BallsQuality { get; init; }

    [Range(1, 10)]
    public int CueQuality { get; init; }

    [Range(1, 10)]
    public int PriceValue { get; init; }

    [Range(1, 10)]
    public int Lighting { get; init; }

    [MaxLength(500)]
    public string? Comment { get; init; }
}
