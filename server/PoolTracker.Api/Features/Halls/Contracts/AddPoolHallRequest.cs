using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Halls.Contracts;

public sealed class AddPoolHallRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(260)]
    public string Address { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(1, 200)]
    public int TotalTables { get; init; }
}
