using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Duels.Contracts;

public sealed class ChooseCoinSideRequest
{
    [Required]
    public CoinSideView Side { get; init; }
}
