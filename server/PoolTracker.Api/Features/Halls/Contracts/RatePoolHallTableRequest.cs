using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Halls.Contracts;

public sealed class RatePoolHallTableRequest
{
    [Range(1, 10)]
    public int ClothQuality { get; init; }

    [Range(1, 10)]
    public int CushionQuality { get; init; }

    [Range(1, 10)]
    public int Levelness { get; init; }

    [MaxLength(500)]
    public string? Comment { get; init; }
}
