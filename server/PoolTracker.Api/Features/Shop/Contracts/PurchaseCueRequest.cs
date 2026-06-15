using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Shop.Contracts;

public sealed class PurchaseCueRequest
{
    [Required]
    public Guid CueItemId { get; init; }
}
