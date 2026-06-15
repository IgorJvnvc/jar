using System.ComponentModel.DataAnnotations;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Features.Notifications.Contracts;

public sealed class RegisterDeviceTokenRequest
{
    [Required]
    [MaxLength(512)]
    public string Token { get; init; } = string.Empty;

    [Required]
    public DevicePlatform Platform { get; init; }
}
