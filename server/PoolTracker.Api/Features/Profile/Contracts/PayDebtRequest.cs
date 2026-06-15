using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Profile.Contracts;

public sealed class PayDebtRequest
{
    [Range(1, 100000)]
    public int Amount { get; init; }
}
