using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Auth.Contracts;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
