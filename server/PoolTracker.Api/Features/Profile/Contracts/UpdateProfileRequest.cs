using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Profile.Contracts;

public sealed class UpdateProfileRequest
{
    [Required]
    [MaxLength(60)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    public string AvatarColorHex { get; init; } = "#1d7a59";

    [Range(1, 15)]
    public int? FavoriteBallNumber { get; init; }
}
