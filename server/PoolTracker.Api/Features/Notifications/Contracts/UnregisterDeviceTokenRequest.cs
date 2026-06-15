using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Notifications.Contracts;

public sealed class UnregisterDeviceTokenRequest
{
    [Required]
    [MaxLength(512)]
    public string Token { get; init; } = string.Empty;
}
