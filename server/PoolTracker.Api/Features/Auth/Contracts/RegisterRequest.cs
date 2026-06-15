using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Auth.Contracts;

public sealed class RegisterRequest
{
    [Required]
    [MaxLength(60)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;
}
