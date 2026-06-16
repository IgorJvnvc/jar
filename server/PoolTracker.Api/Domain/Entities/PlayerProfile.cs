namespace PoolTracker.Api.Domain.Entities;

public sealed class PlayerProfile
{
    public const string DebtTitle = "Himen Healer";

    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string AvatarColorHex { get; set; } = "#1d7a59";

    public int? FavoriteBallNumber { get; set; }

    public int Points { get; set; }

    public int DebtPoints { get; set; }

    public string? Title { get; set; }

    public decimal Power { get; set; } = 50;

    public decimal Accuracy { get; set; } = 50;

    public decimal CueControl { get; set; } = 50;

    public decimal Spin { get; set; } = 50;

    public int DuelsWon { get; set; }

    public int DuelsLost { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
