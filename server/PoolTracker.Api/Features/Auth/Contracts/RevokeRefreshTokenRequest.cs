using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Auth.Contracts;

public sealed class RevokeRefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
