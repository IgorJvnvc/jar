using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Halls.Contracts;

public sealed class AddPoolHallTableRequest
{
    [Required]
    [MaxLength(80)]
    public string TableLabel { get; init; } = string.Empty;
}
