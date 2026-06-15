using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Sessions.Contracts;

public sealed class StartSessionRequest
{
    [Required]
    public Guid PoolHallId { get; init; }

    public Guid? PoolHallTableId { get; init; }
}
