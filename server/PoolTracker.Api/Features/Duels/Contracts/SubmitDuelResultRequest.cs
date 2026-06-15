using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Duels.Contracts;

public sealed class SubmitDuelResultRequest
{
    [Required]
    public DuelResultChoiceView Choice { get; init; }
}
