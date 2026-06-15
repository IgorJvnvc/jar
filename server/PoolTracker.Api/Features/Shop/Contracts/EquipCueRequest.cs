using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Shop.Contracts;

public sealed class EquipCueRequest
{
    [Required]
    public Guid CueItemId { get; init; }
}
