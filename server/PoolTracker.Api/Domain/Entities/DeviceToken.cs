namespace PoolTracker.Api.Domain.Entities;

public enum DevicePlatform
{
    Web = 1,
    Android = 2,
    Ios = 3
}

public sealed class DeviceToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
